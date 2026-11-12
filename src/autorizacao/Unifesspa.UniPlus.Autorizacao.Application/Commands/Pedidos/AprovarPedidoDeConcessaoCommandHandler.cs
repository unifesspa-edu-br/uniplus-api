namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler da aprovação; a regra da etapa é de <see cref="AprovacaoDoPedido"/>. A janela de migração vencida e ainda não
/// materializada é materializada antes das travas: sem isso, a concessão antiga continuaria não revogada e a nova
/// esbarraria nela; só quem decide alguma etapa provoca essa gravação, e quem não decide segue para a recusa de acesso
/// do <c>Handle</c>.
/// </summary>
public static class AprovarPedidoDeConcessaoCommandHandler
{
    public static async Task Before(
        AprovarPedidoDeConcessaoCommand command,
        IPedidoDeConcessaoRepository pedidos,
        MaterializacaoPrevia previa,
        IVerificadorDeAcesso verificador,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(pedidos);
        ArgumentNullException.ThrowIfNull(previa);

        AcessoDoAprovador? acesso = await AcessoDoAprovador.ConsultarAsync(verificador, false, command.Id, cancellationToken).ConfigureAwait(false);
        if (acesso is { Aprova: true } && await pedidos.ObterPerfilIdAsync(command.Id, cancellationToken).ConfigureAwait(false) is { } perfilId)
        {
            await previa.GarantirAsync(perfilId, cancellationToken).ConfigureAwait(false);
        }
    }

    public static Task<(Result<PedidoDeConcessaoDto> Resposta, IEnumerable<object> Mensagens)> Handle(
        AprovarPedidoDeConcessaoCommand command,
        AprovacaoDoPedido aprovacao,
        IVerificadorDeAcesso verificador,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(aprovacao);
        ArgumentNullException.ThrowIfNull(userContext);

        return aprovacao.AprovarAsync(command, verificador, IdentidadeDoToken.De(userContext), cancellationToken);
    }
}
