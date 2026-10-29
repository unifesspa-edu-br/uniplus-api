namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Handler da recusa. Encerra o pedido com o motivo, sem concessão.</summary>
public static class RecusarPedidoDeConcessaoCommandHandler
{
    public static async Task<Result<PedidoDeConcessaoDto>> Handle(
        RecusarPedidoDeConcessaoCommand command,
        IPedidoDeConcessaoRepository pedidos,
        IConcessaoRepository concessoes,
        IPerfilRepository perfis,
        IVerificadorDeAcesso verificador,
        CatalogoDaAplicacao catalogo,
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
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        Result<DecisaoDaEtapa> preparada = await DecisaoDaEtapa
            .PrepararAsync(command.Id, pedidos, concessoes, verificador, catalogo, IdentidadeDoToken.De(userContext), cancellationToken)
            .ConfigureAwait(false);
        if (preparada.IsFailure)
        {
            return Result<PedidoDeConcessaoDto>.Failure(preparada.Error!);
        }

        DecisaoDaEtapa decisao = preparada.Value!;

        Result recusa = decisao.Pedido.Recusar(decisao.Decisor, command.Motivo, decisao.Avaliado, timeProvider.GetUtcNow());
        if (recusa.IsFailure)
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<PedidoDeConcessaoDto>.ValidationFailure(recusa.Errors);
        }

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ViolacoesDeGravacao.EhConflitoDeConcorrencia(ex))
        {
            // Com Idempotency-Key o handler captura e descarta o rastreamento (ADR-0119).
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<PedidoDeConcessaoDto>.Failure(PedidoHandlerErros.ConflitoDeConcorrencia());
        }

        return Result<PedidoDeConcessaoDto>.Success(
            await RespostaDoPedido.ParaDtoAsync(decisao.Pedido, perfis, cancellationToken).ConfigureAwait(false));
    }
}
