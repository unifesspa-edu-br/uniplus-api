namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O aceite do pedido pelo beneficiário e, no pedido protegido, do termo fixado nele. O beneficiário ainda desconhecido é
/// conferido pelo e-mail verificado do pedido, e o já conhecido, pela identidade do token; senão, o aceite é recusado. O
/// aceite não toca a vigência do termo: compara a versão apresentada com a fixada, que é imutável.
/// No pedido feito para outra pessoa, é no aceite que a identidade do beneficiário passa a ser conhecida, e a
/// incompatibilidade dos papéis de governança se confere aqui, sem trava: o aceite não concede, e a conferência
/// definitiva é a da aprovação, sob a trava da pessoa (ADR-0162). O aviso à fila do gestor sai como mensagem de saída, no
/// outbox da mesma transação.
/// </summary>
public sealed class AceiteDoPedido
{
    private readonly IPedidoDeConcessaoRepository _pedidos;
    private readonly IPerfilRepository _perfis;
    private readonly AvisosDoPedido _avisos;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIncompatibilidadeDosPapeis _papeis;
    private readonly TimeProvider _clock;

    public AceiteDoPedido(
        IPedidoDeConcessaoRepository pedidos,
        IPerfilRepository perfis,
        AvisosDoPedido avisos,
        IUnitOfWork unitOfWork,
        IIncompatibilidadeDosPapeis papeis,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(pedidos);
        ArgumentNullException.ThrowIfNull(perfis);
        ArgumentNullException.ThrowIfNull(avisos);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(papeis);
        ArgumentNullException.ThrowIfNull(clock);

        _pedidos = pedidos;
        _perfis = perfis;
        _avisos = avisos;
        _unitOfWork = unitOfWork;
        _papeis = papeis;
        _clock = clock;
    }

    internal async Task<(Result<PedidoDeConcessaoDto> Resposta, IEnumerable<object> Mensagens)> AceitarAsync(
        Guid pedidoId, PessoaNoToken pessoa, Guid? termoApresentado, CancellationToken cancellationToken)
    {
        PedidoDeConcessao? pedido = await _pedidos.ObterParaAlterarAsync(pedidoId, cancellationToken).ConfigureAwait(false);
        if (pedido is null)
        {
            return Recusa(PedidoHandlerErros.NaoEncontrado());
        }

        if (await RecusaDoAceiteAsync(pedido, pessoa, termoApresentado, cancellationToken).ConfigureAwait(false) is { } recusa)
        {
            _unitOfWork.DescartarAlteracoesNaoSalvas();
            return (Result<PedidoDeConcessaoDto>.ValidationFailure(recusa.Errors), []);
        }

        return await GravarAsync(pedido, cancellationToken).ConfigureAwait(false);
    }

    private static (Result<PedidoDeConcessaoDto> Resposta, IEnumerable<object> Mensagens) Recusa(DomainError erro) =>
        (Result<PedidoDeConcessaoDto>.Failure(erro), []);

    private async Task<Result?> RecusaDoAceiteAsync(
        PedidoDeConcessao pedido, PessoaNoToken pessoa, Guid? termoApresentado, CancellationToken cancellationToken)
    {
        DateTimeOffset agora = _clock.GetUtcNow();
        Result aceite = pedido.Aceitar(pessoa, termoApresentado, agora);
        if (aceite.IsFailure)
        {
            return aceite;
        }

        return await _papeis.TemPapelIncompativelAsync(pessoa.Identidade, pedido.PerfilId, agora, cancellationToken).ConfigureAwait(false)
            ? Result.Failure(PedidoHandlerErros.PapeisIncompativeis())
            : null;
    }

    private async Task<(Result<PedidoDeConcessaoDto> Resposta, IEnumerable<object> Mensagens)> GravarAsync(
        PedidoDeConcessao pedido, CancellationToken cancellationToken)
    {
        if (!await SalvouAsync(cancellationToken).ConfigureAwait(false))
        {
            return Recusa(PedidoHandlerErros.ConflitoDeConcorrencia());
        }

        PedidoDeConcessaoDto dto = await RespostaDoPedido.ParaDtoAsync(pedido, _perfis, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<EnviarEmail> mensagens = await _avisos
            .AvisarAsync(pedido, dto.Perfil.Nome, SituacaoDoPedido.AguardandoBeneficiario, cancellationToken)
            .ConfigureAwait(false);

        return (Result<PedidoDeConcessaoDto>.Success(dto), mensagens);
    }

    // Com Idempotency-Key o handler captura e descarta o rastreamento (ADR-0119).
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
