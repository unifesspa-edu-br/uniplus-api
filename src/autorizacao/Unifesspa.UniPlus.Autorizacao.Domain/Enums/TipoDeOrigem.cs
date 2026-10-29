namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>O que gerou a concessão: um pedido aprovado ou a instalação da plataforma.</summary>
public enum TipoDeOrigem
{
    /// <summary>Pedido de acesso aprovado.</summary>
    Pedido = 1,

    /// <summary>Instalação da plataforma, que designa os primeiros gestores de acessos.</summary>
    Instalacao = 2,
}

/// <summary>Código textual canônico de <see cref="TipoDeOrigem"/>, o valor gravado e exposto.</summary>
public static class TipoDeOrigemCodigo
{
    public const string Pedido = "pedido";
    public const string Instalacao = "instalacao";

    public static string ToCodigo(this TipoDeOrigem tipo) => tipo switch
    {
        TipoDeOrigem.Pedido => Pedido,
        TipoDeOrigem.Instalacao => Instalacao,
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, null),
    };

    public static TipoDeOrigem FromCodigo(string codigo) => codigo switch
    {
        Pedido => TipoDeOrigem.Pedido,
        Instalacao => TipoDeOrigem.Instalacao,
        _ => throw new ArgumentOutOfRangeException(nameof(codigo), codigo, null),
    };
}
