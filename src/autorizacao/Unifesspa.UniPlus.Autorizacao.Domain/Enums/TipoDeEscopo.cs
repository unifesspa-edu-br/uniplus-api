namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>Alcance de uma concessão: uma unidade organizacional ou a instituição inteira.</summary>
public enum TipoDeEscopo
{
    /// <summary>A concessão vale numa unidade; não alcança as unidades abaixo dela (ADR-0079).</summary>
    Unidade = 1,

    /// <summary>A concessão vale para recursos da instituição.</summary>
    Instituicao = 2,
}

/// <summary>Código textual canônico de <see cref="TipoDeEscopo"/>, o valor gravado e exposto.</summary>
public static class TipoDeEscopoCodigo
{
    public const string Unidade = "unidade";
    public const string Instituicao = "instituicao";

    public static string ToCodigo(this TipoDeEscopo tipo) => tipo switch
    {
        TipoDeEscopo.Unidade => Unidade,
        TipoDeEscopo.Instituicao => Instituicao,
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, null),
    };

    public static TipoDeEscopo FromCodigo(string codigo) => codigo switch
    {
        Unidade => TipoDeEscopo.Unidade,
        Instituicao => TipoDeEscopo.Instituicao,
        _ => throw new ArgumentOutOfRangeException(nameof(codigo), codigo, null),
    };
}
