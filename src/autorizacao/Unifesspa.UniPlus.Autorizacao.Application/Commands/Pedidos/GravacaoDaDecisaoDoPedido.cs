namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Concessoes;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A gravação da aprovação de uma etapa: a concessão da última aprovação exigida, a resposta com o pedido e os avisos da
/// situação em que ele ficou. A concessão igual vencida e não revogada é revogada antes, porque o índice único a trata
/// como existente; a ativa igual recusa o pedido.
/// </summary>
public sealed class GravacaoDaDecisaoDoPedido
{
    private readonly IAutorizacaoUnitOfWork _unitOfWork;
    private readonly IPerfilRepository _perfis;
    private readonly AvisosDoPedido _avisos;
    private readonly ConcessaoDoPedidoAprovado _concessao;

    public GravacaoDaDecisaoDoPedido(
        IAutorizacaoUnitOfWork unitOfWork, IPerfilRepository perfis, AvisosDoPedido avisos, ConcessaoDoPedidoAprovado concessao)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(perfis);
        ArgumentNullException.ThrowIfNull(avisos);
        ArgumentNullException.ThrowIfNull(concessao);

        _unitOfWork = unitOfWork;
        _perfis = perfis;
        _avisos = avisos;
        _concessao = concessao;
    }

    /// <summary>A recusa sem gravação nem aviso.</summary>
    public static (Result<PedidoDeConcessaoDto> Resposta, IEnumerable<object> Mensagens) Recusa(DomainError erro) =>
        (Result<PedidoDeConcessaoDto>.Failure(erro), []);

    /// <summary>
    /// A aprovação recusada pelo agregado. O perfil alterado ou o prazo vencido entre as etapas devolvem o pedido ao
    /// gestor, e o perfil que passou a exigir o termo devolve o pedido ao solicitante; esse estado se grava. As demais
    /// recusas descartam o rastreamento.
    /// </summary>
    internal async Task<(Result<PedidoDeConcessaoDto> Resposta, IEnumerable<object> Mensagens)> RecusarAprovacaoAsync(
        DecisaoDaEtapa decisao, IReadOnlyList<FieldError> erros, CancellationToken cancellationToken)
    {
        DomainError erro = erros[0].Error;
        if (erro.Code is PedidoErrorCodes.PerfilAlterado or PedidoErrorCodes.PrazoVencido or PedidoErrorCodes.TermoExigido)
        {
            return await GravarDevolucaoAsync(decisao, erro, cancellationToken).ConfigureAwait(false);
        }

        _unitOfWork.DescartarAlteracoesNaoSalvas();
        return (Result<PedidoDeConcessaoDto>.ValidationFailure(erros), []);
    }

    /// <summary>Grava a aprovação aceita e, quando ela conclui o pedido, a concessão que dele nasce.</summary>
    internal async Task<(Result<PedidoDeConcessaoDto> Resposta, IEnumerable<object> Mensagens)> GravarAsync(
        DecisaoDaEtapa decisao, SituacaoDoPedido situacaoAnterior, bool concluido, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        Result gravado = await TentarGravarAsync(decisao, concluido, agora, cancellationToken).ConfigureAwait(false);
        if (gravado.IsFailure)
        {
            _unitOfWork.DescartarAlteracoesNaoSalvas();
            return (Result<PedidoDeConcessaoDto>.ValidationFailure(gravado.Errors), []);
        }

        PedidoDeConcessaoDto dto = await RespostaDoPedido.ParaDtoAsync(decisao.Pedido, _perfis, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<EnviarEmail> mensagens = await _avisos
            .AvisarAsync(decisao.Pedido, dto.Perfil.Nome, situacaoAnterior, cancellationToken)
            .ConfigureAwait(false);

        return (Result<PedidoDeConcessaoDto>.Success(dto), mensagens);
    }

    private async Task<Result> TentarGravarAsync(DecisaoDaEtapa decisao, bool concluido, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        try
        {
            return await ConcederESalvarAsync(decisao, concluido, agora, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ConcessaoViolacoes.EhConcessaoDuplicada(ex))
        {
            // Duas aprovações correram juntas, ou a concessão igual nasceu entre a leitura e a gravação.
            return Result.Failure(PedidoHandlerErros.JaTemConcessao());
        }
        catch (Exception ex) when (ViolacoesDeGravacao.EhConflitoDeConcorrencia(ex))
        {
            // Com Idempotency-Key o handler captura e descarta o rastreamento (ADR-0119).
            return Result.Failure(PedidoHandlerErros.ConflitoDeConcorrencia());
        }
    }

    private async Task<Result> ConcederESalvarAsync(DecisaoDaEtapa decisao, bool concluido, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        Result concedida = concluido
            ? await _concessao.ConcederAsync(decisao, agora, cancellationToken).ConfigureAwait(false)
            : Result.Success();
        if (concedida.IsSuccess)
        {
            await _unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }

        return concedida;
    }

    // O pedido voltou ao gestor: a fila dele é avisada, mesmo que a resposta a quem decidiu a segunda etapa seja a recusa. O
    // pedido devolvido por exigir o termo não gera aviso.
    private async Task<(Result<PedidoDeConcessaoDto> Resposta, IEnumerable<object> Mensagens)> GravarDevolucaoAsync(
        DecisaoDaEtapa decisao, DomainError erro, CancellationToken cancellationToken)
    {
        if (!await SalvouAsync(cancellationToken).ConfigureAwait(false))
        {
            return Recusa(PedidoHandlerErros.ConflitoDeConcorrencia());
        }

        IReadOnlyList<EnviarEmail> mensagens = await _avisos
            .AvisarAsync(decisao.Pedido, decisao.Perfil.Nome, SituacaoDoPedido.AguardandoProtecaoDeDados, cancellationToken)
            .ConfigureAwait(false);

        return (Result<PedidoDeConcessaoDto>.Failure(erro), mensagens);
    }

    private async Task<bool> SalvouAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ViolacoesDeGravacao.EhConflitoDeConcorrencia(ex))
        {
            _unitOfWork.DescartarAlteracoesNaoSalvas();
            return false;
        }
    }
}
