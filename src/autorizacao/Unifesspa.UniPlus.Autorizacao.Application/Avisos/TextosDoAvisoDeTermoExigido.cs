namespace Unifesspa.UniPlus.Autorizacao.Application.Avisos;

/// <summary>O texto do aviso do pedido devolvido porque o perfil passou a exigir o termo de responsabilidade.</summary>
internal static class TextosDoAvisoDeTermoExigido
{
    public static readonly KeyValuePair<TipoDeAviso, TextoDoAviso>[] Todos =
    [
        TextoDoAviso.Para(
            TipoDeAviso.PedidoDevolvidoPorTermoExigido,
            "Pedido de acesso devolvido: o perfil passou a exigir o termo de responsabilidade",
            "O perfil pedido passou a exigir o aceite do termo de responsabilidade depois que o pedido foi feito. O pedido foi devolvido; refaça-o para ler e aceitar o termo."),
    ];
}
