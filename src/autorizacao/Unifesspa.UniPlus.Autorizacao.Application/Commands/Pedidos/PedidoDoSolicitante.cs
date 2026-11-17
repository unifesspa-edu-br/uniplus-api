namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>O pedido que só o solicitante altera: o de outra pessoa não se distingue do inexistente.</summary>
internal static class PedidoDoSolicitante
{
    /// <summary>O pedido lido para alterar; nulo quando não existe ou é de outra pessoa.</summary>
    public static async Task<PedidoDeConcessao?> ObterParaAlterarAsync(
        IPedidoDeConcessaoRepository pedidos, Guid pedidoId, Identidade solicitante, CancellationToken cancellationToken)
    {
        PedidoDeConcessao? pedido = await pedidos.ObterParaAlterarAsync(pedidoId, cancellationToken).ConfigureAwait(false);

        return pedido is not null && pedido.Solicitante.Eh(solicitante) ? pedido : null;
    }
}
