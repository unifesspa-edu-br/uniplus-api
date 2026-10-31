namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.ExternalServices;

using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;

/// <summary>O link do pedido na Configuração: <c>{endereço}/acessos/pedidos/{id}</c>.</summary>
internal sealed class LinkDoPedido : ILinkDoPedido
{
    private readonly string _base;

    public LinkDoPedido(IOptions<AvisosDoPedidoOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _base = options.Value.EnderecoDaConfiguracao.TrimEnd('/');
    }

    public string Para(Guid pedidoId) => $"{_base}/acessos/pedidos/{pedidoId}";
}
