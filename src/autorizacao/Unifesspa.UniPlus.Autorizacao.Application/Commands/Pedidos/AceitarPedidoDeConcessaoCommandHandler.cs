namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Handler do aceite; a regra do aceite é de <see cref="AceiteDoPedido"/>.</summary>
public static class AceitarPedidoDeConcessaoCommandHandler
{
    public static async Task<(Result<PedidoDeConcessaoDto> Resposta, IEnumerable<object> Mensagens)> Handle(
        AceitarPedidoDeConcessaoCommand command,
        AceiteDoPedido aceite,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(aceite);
        ArgumentNullException.ThrowIfNull(userContext);

        return IdentidadeDoToken.PessoaDe(userContext) is { } pessoa
            ? await aceite.AceitarAsync(command.Id, pessoa, cancellationToken).ConfigureAwait(false)
            : (Result<PedidoDeConcessaoDto>.Failure(PedidoHandlerErros.AutorNaoIdentificado()), []);
    }
}
