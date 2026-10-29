namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>Etapa de aprovação do pedido. A primeira é sempre a do gestor.</summary>
public enum EtapaDoPedido
{
    /// <summary>Adequação funcional, a cargo do gestor de acessos.</summary>
    Gestor = 1,

    /// <summary>Adequação à proteção de dados, a cargo da encarregada de dados.</summary>
    Encarregada = 2,
}

/// <summary>Código textual canônico de <see cref="EtapaDoPedido"/>, o valor gravado e exposto.</summary>
public static class EtapaDoPedidoCodigo
{
    public const string Gestor = "gestor";
    public const string Encarregada = "encarregada";

    public static string ToCodigo(this EtapaDoPedido etapa) => etapa switch
    {
        EtapaDoPedido.Gestor => Gestor,
        EtapaDoPedido.Encarregada => Encarregada,
        _ => throw new ArgumentOutOfRangeException(nameof(etapa), etapa, null),
    };

    public static EtapaDoPedido FromCodigo(string codigo) => codigo switch
    {
        Gestor => EtapaDoPedido.Gestor,
        Encarregada => EtapaDoPedido.Encarregada,
        _ => throw new ArgumentOutOfRangeException(nameof(codigo), codigo, null),
    };
}
