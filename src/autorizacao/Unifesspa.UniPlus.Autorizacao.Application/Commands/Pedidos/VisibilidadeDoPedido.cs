namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Quem vê o pedido: as partes (o solicitante e o beneficiário, este também pelo e-mail verificado antes de aceitar) e
/// quem decide uma das etapas ou administra concessões. Para os demais, o pedido não se distingue do inexistente.
/// </summary>
internal static class VisibilidadeDoPedido
{
    /// <summary>
    /// Confere a visibilidade, ou devolve o erro que a borda responde: 401 para a identidade incompleta, 404 para
    /// quem não vê.
    /// </summary>
    public static async Task<DomainError?> ConferirAsync(
        PedidoDeConcessao pedido, PessoaNoToken pessoa, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        if (pedido.EhParte(pessoa))
        {
            return null;
        }

        AcessoDoAprovador? acesso = await AcessoDoAprovador.ConsultarAsync(verificador, incluirAdministrar: true, cancellationToken)
            .ConfigureAwait(false);

        return acesso is null
            ? PedidoHandlerErros.IdentidadeIncompleta()
            : acesso.VeTodos ? null : PedidoHandlerErros.NaoEncontrado();
    }
}
