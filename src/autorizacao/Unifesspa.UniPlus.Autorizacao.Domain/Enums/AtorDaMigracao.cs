namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>Quem tomou a decisão da janela: uma pessoa ou o sistema.</summary>
public enum AtorDaMigracao
{
    Humano = 1,
    Sistema = 2,
}

/// <summary>Código textual canônico de <see cref="AtorDaMigracao"/>, o valor gravado e exposto.</summary>
public static class AtorDaMigracaoCodigo
{
    public const string Humano = "humano";
    public const string Sistema = "sistema";

    public static string ToCodigo(this AtorDaMigracao ator) => ator switch
    {
        AtorDaMigracao.Humano => Humano,
        AtorDaMigracao.Sistema => Sistema,
        _ => throw new ArgumentOutOfRangeException(nameof(ator), ator, null),
    };

    public static AtorDaMigracao FromCodigo(string codigo) => codigo switch
    {
        Humano => AtorDaMigracao.Humano,
        Sistema => AtorDaMigracao.Sistema,
        _ => throw new ArgumentOutOfRangeException(nameof(codigo), codigo, null),
    };
}
