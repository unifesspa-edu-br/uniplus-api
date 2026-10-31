namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

/// <summary>Monta a resposta do pedido, com o nome do perfil e se ele admite prazo.</summary>
internal static class RespostaDoPedido
{
    public static async Task<PedidoDeConcessaoDto> ParaDtoAsync(
        PedidoDeConcessao pedido, IPerfilRepository perfis, CancellationToken cancellationToken)
    {
        Perfil? perfil = await perfis.ObterPorIdAsync(pedido.PerfilId, cancellationToken).ConfigureAwait(false);

        return PedidoMapping.ToDto(pedido, perfil);
    }
}
