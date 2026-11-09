namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>Quem retirou a versão pendente: uma pessoa, com motivo, ou a contenção, que a cancela sozinha.</summary>
public enum FormaDoCancelamento
{
    /// <summary>Quem criou a versão, ou quem mantém perfis, a retirou com um motivo.</summary>
    Pessoa = 1,

    /// <summary>Uma remoção de permissões cancelou a pendente.</summary>
    Contencao = 2,
}

/// <summary>Código textual canônico de <see cref="FormaDoCancelamento"/>, o valor gravado e exposto.</summary>
public static class FormaDoCancelamentoCodigo
{
    public const string Pessoa = "pessoa";
    public const string Contencao = "contencao";

    public static string ToCodigo(this FormaDoCancelamento forma) => forma switch
    {
        FormaDoCancelamento.Pessoa => Pessoa,
        FormaDoCancelamento.Contencao => Contencao,
        _ => throw new ArgumentOutOfRangeException(nameof(forma), forma, null),
    };

    public static FormaDoCancelamento FromCodigo(string codigo) => codigo switch
    {
        Pessoa => FormaDoCancelamento.Pessoa,
        Contencao => FormaDoCancelamento.Contencao,
        _ => throw new ArgumentOutOfRangeException(nameof(codigo), codigo, null),
    };
}
