namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>O que uma etapa decidiu sobre a permissão reclassificada numa concessão.</summary>
public enum ResultadoDaLiberacao
{
    /// <summary>A etapa libera a permissão.</summary>
    Liberada = 1,

    /// <summary>A etapa mantém a permissão suspensa, com motivo.</summary>
    Mantida = 2,
}

/// <summary>Código textual canônico de <see cref="ResultadoDaLiberacao"/>, o valor gravado e exposto.</summary>
public static class ResultadoDaLiberacaoCodigo
{
    public const string Liberada = "liberada";
    public const string Mantida = "mantida";

    public static string ToCodigo(this ResultadoDaLiberacao valor) => valor switch
    {
        ResultadoDaLiberacao.Liberada => Liberada,
        ResultadoDaLiberacao.Mantida => Mantida,
        _ => throw new ArgumentOutOfRangeException(nameof(valor), valor, null),
    };

    public static ResultadoDaLiberacao FromCodigo(string codigo) => codigo switch
    {
        Liberada => ResultadoDaLiberacao.Liberada,
        Mantida => ResultadoDaLiberacao.Mantida,
        _ => throw new ArgumentOutOfRangeException(nameof(codigo), codigo, null),
    };

    public static ResultadoDaLiberacao? TentarLer(string? codigo) => codigo switch
    {
        Liberada => ResultadoDaLiberacao.Liberada,
        Mantida => ResultadoDaLiberacao.Mantida,
        _ => null,
    };
}
