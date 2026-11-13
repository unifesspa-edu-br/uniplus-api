namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>O que o Encarregado de dados pede sobre uma concessão (UNI-REQ-0375).</summary>
public enum TipoDaProvidencia
{
    /// <summary>A retirada do acesso.</summary>
    Retirada = 1,

    /// <summary>A revisão do acesso.</summary>
    Revisao = 2,
}

/// <summary>Código textual canônico de <see cref="TipoDaProvidencia"/>, o valor gravado, exposto e aceito no corpo.</summary>
public static class TipoDaProvidenciaCodigo
{
    public const string Retirada = "retirada";
    public const string Revisao = "revisao";

    public static string ToCodigo(this TipoDaProvidencia tipo) => tipo switch
    {
        TipoDaProvidencia.Retirada => Retirada,
        TipoDaProvidencia.Revisao => Revisao,
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, null),
    };

    /// <summary>O tipo gravado; o código desconhecido é defeito da gravação.</summary>
    public static TipoDaProvidencia FromCodigo(string codigo) =>
        DoCodigo(codigo) ?? throw new ArgumentOutOfRangeException(nameof(codigo), codigo, null);

    /// <summary>O tipo do código informado, sem distinguir maiúsculas nem espaços nas pontas; nulo quando não existe.</summary>
    public static TipoDaProvidencia? DoCodigo(string? codigo) => codigo?.Trim().ToLowerInvariant() switch
    {
        Retirada => TipoDaProvidencia.Retirada,
        Revisao => TipoDaProvidencia.Revisao,
        _ => null,
    };
}
