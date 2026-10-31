namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler da recusa. Encerra o pedido com o motivo, sem concessão. O aviso do desfecho sai como mensagem de saída, no
/// outbox da mesma transação, e não leva o motivo.
/// </summary>
public static class RecusarPedidoDeConcessaoCommandHandler
{
    public static async Task<(Result<PedidoDeConcessaoDto> Resposta, IEnumerable<object> Mensagens)> Handle(
        RecusarPedidoDeConcessaoCommand command,
        IPedidoDeConcessaoRepository pedidos,
        IConcessaoRepository concessoes,
        IPerfilRepository perfis,
        IVerificadorDeAcesso verificador,
        CatalogoDaAplicacao catalogo,
        AvisosDoPedido avisos,
        IAutorizacaoUnitOfWork unitOfWork,
        IUserContext userContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(pedidos);
        ArgumentNullException.ThrowIfNull(concessoes);
        ArgumentNullException.ThrowIfNull(perfis);
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(avisos);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        Result<DecisaoDaEtapa> preparada = await DecisaoDaEtapa
            .PrepararAsync(command.Id, pedidos, concessoes, verificador, catalogo, IdentidadeDoToken.De(userContext), cancellationToken)
            .ConfigureAwait(false);
        if (preparada.IsFailure)
        {
            return Recusa(preparada.Error!);
        }

        DecisaoDaEtapa decisao = preparada.Value!;
        SituacaoDoPedido situacaoAnterior = decisao.Pedido.Situacao;

        Result recusa = decisao.Pedido.Recusar(decisao.Decisor, command.Motivo, decisao.Avaliado, timeProvider.GetUtcNow());
        if (recusa.IsFailure)
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return (Result<PedidoDeConcessaoDto>.ValidationFailure(recusa.Errors), []);
        }

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ViolacoesDeGravacao.EhConflitoDeConcorrencia(ex))
        {
            // Com Idempotency-Key o handler captura e descarta o rastreamento (ADR-0119).
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Recusa(PedidoHandlerErros.ConflitoDeConcorrencia());
        }

        PedidoDeConcessaoDto dto = await RespostaDoPedido.ParaDtoAsync(decisao.Pedido, perfis, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<EnviarEmail> mensagens = await avisos
            .AvisarAsync(decisao.Pedido, dto.Perfil.Nome, situacaoAnterior, cancellationToken)
            .ConfigureAwait(false);

        return (Result<PedidoDeConcessaoDto>.Success(dto), mensagens);
    }

    private static (Result<PedidoDeConcessaoDto> Resposta, IEnumerable<object> Mensagens) Recusa(DomainError erro) =>
        (Result<PedidoDeConcessaoDto>.Failure(erro), []);
}
