namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do cancelamento. O pedido de outra pessoa não se distingue do inexistente. O endpoint não leva
/// <c>Idempotency-Key</c>: o conflito de concorrência propaga e o middleware o responde como 409 (ADR-0119).
/// </summary>
public static class CancelarPedidoDeConcessaoCommandHandler
{
    public static async Task<Result> Handle(
        CancelarPedidoDeConcessaoCommand command,
        IPedidoDeConcessaoRepository repository,
        IAutorizacaoUnitOfWork unitOfWork,
        IUserContext userContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (IdentidadeDoToken.De(userContext) is not { } solicitante)
        {
            return Result.Failure(PedidoHandlerErros.AutorNaoIdentificado());
        }

        PedidoDeConcessao? pedido = await repository.ObterParaAlterarAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (pedido is null || pedido.Solicitante != solicitante)
        {
            return Result.Failure(PedidoHandlerErros.NaoEncontrado());
        }

        Result cancelado = pedido.Cancelar(timeProvider.GetUtcNow());
        if (cancelado.IsFailure)
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return cancelado;
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
