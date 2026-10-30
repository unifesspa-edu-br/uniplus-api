namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>Etapa de aprovação do pedido. A primeira é sempre a do gestor.</summary>
public enum EtapaDoPedido
{
    /// <summary>Adequação funcional, a cargo do gestor de acessos.</summary>
    Gestor = 1,

    /// <summary>Adequação à proteção de dados, a cargo da proteção de dados.</summary>
    ProtecaoDeDados = 2,
}

/// <summary>Código textual canônico de <see cref="EtapaDoPedido"/>, o valor gravado e exposto.</summary>
public static class EtapaDoPedidoCodigo
{
    public const string Gestor = "gestor";
    public const string ProtecaoDeDados = "protecao_de_dados";

    public static string ToCodigo(this EtapaDoPedido etapa) => etapa switch
    {
        EtapaDoPedido.Gestor => Gestor,
        EtapaDoPedido.ProtecaoDeDados => ProtecaoDeDados,
        _ => throw new ArgumentOutOfRangeException(nameof(etapa), etapa, null),
    };

    public static EtapaDoPedido FromCodigo(string codigo) => codigo switch
    {
        Gestor => EtapaDoPedido.Gestor,
        ProtecaoDeDados => EtapaDoPedido.ProtecaoDeDados,
        _ => throw new ArgumentOutOfRangeException(nameof(codigo), codigo, null),
    };
}
