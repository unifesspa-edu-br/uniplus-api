namespace Unifesspa.UniPlus.Autorizacao.Application.Abstractions;

/// <summary>O endereço, na Configuração, em que a pessoa abre o pedido que o e-mail avisa.</summary>
public interface ILinkDoPedido
{
    string Para(Guid pedidoId);
}
