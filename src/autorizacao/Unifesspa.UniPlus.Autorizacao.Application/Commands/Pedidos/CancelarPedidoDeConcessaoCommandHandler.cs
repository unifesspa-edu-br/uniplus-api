namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Handler do cancelamento; a regra do cancelamento é de <see cref="CancelamentoDoPedido"/>.</summary>
public static class CancelarPedidoDeConcessaoCommandHandler
{
    public static async Task<(Result Resposta, IEnumerable<object> Mensagens)> Handle(
        CancelarPedidoDeConcessaoCommand command,
        CancelamentoDoPedido cancelamento,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(cancelamento);
        ArgumentNullException.ThrowIfNull(userContext);

        return IdentidadeDoToken.De(userContext) is { } solicitante
            ? await cancelamento.CancelarAsync(command.Id, solicitante, cancellationToken).ConfigureAwait(false)
            : (Result.Failure(PedidoHandlerErros.AutorNaoIdentificado()), []);
    }
}
