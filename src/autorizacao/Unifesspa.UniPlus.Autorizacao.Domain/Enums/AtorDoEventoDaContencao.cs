namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>Quem praticou o evento da contenção: uma pessoa ou o próprio sistema, que fecha a suspensão sozinho.</summary>
public enum AtorDoEventoDaContencao
{
    /// <summary>Uma pessoa identificada pelo token.</summary>
    Humano = 1,

    /// <summary>A retomada decorre de uma decisão de etapa, de uma troca de vigência ou de uma reclassificação.</summary>
    Sistema = 2,
}

/// <summary>Código textual canônico de <see cref="AtorDoEventoDaContencao"/>, o valor gravado e exposto.</summary>
public static class AtorDoEventoDaContencaoCodigo
{
    public const string Humano = "humano";
    public const string Sistema = "sistema";

    public static string ToCodigo(this AtorDoEventoDaContencao ator) => ator switch
    {
        AtorDoEventoDaContencao.Humano => Humano,
        AtorDoEventoDaContencao.Sistema => Sistema,
        _ => throw new ArgumentOutOfRangeException(nameof(ator), ator, null),
    };

    public static AtorDoEventoDaContencao FromCodigo(string codigo) => codigo switch
    {
        Humano => AtorDoEventoDaContencao.Humano,
        Sistema => AtorDoEventoDaContencao.Sistema,
        _ => throw new ArgumentOutOfRangeException(nameof(codigo), codigo, null),
    };
}
