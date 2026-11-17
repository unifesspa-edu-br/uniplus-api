namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Handler da recusa; a regra da recusa é de <see cref="RecusaDoPedido"/>.</summary>
public static class RecusarPedidoDeConcessaoCommandHandler
{
    public static Task<(Result<PedidoDeConcessaoDto> Resposta, IEnumerable<object> Mensagens)> Handle(
        RecusarPedidoDeConcessaoCommand command,
        RecusaDoPedido recusa,
        IVerificadorDeAcesso verificador,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(recusa);
        ArgumentNullException.ThrowIfNull(userContext);

        return recusa.RecusarAsync(command, verificador, IdentidadeDoToken.De(userContext), cancellationToken);
    }
}
