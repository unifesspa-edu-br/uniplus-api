namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>
/// Papel de governança que um perfil de sistema exerce. Só os perfis de governança
/// carregam um valor; os perfis comuns não têm nenhum.
/// </summary>
public enum TipoDeGovernanca
{
    /// <summary>Opera as concessões: concede, revoga e mantém os perfis.</summary>
    GestorDeAcessos = 1,

    /// <summary>Aprova a concessão de acesso pessoal ou sensível.</summary>
    ProtecaoDeDados = 2,
}

/// <summary>Código textual canônico de <see cref="TipoDeGovernanca"/>, o valor gravado e exposto.</summary>
public static class TipoDeGovernancaCodigo
{
    public const string GestorDeAcessos = "gestor-de-acessos";
    public const string ProtecaoDeDados = "protecao-de-dados";

    public static string ToCodigo(this TipoDeGovernanca tipo) => tipo switch
    {
        TipoDeGovernanca.GestorDeAcessos => GestorDeAcessos,
        TipoDeGovernanca.ProtecaoDeDados => ProtecaoDeDados,
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, null),
    };

    public static TipoDeGovernanca FromCodigo(string codigo) => codigo switch
    {
        GestorDeAcessos => TipoDeGovernanca.GestorDeAcessos,
        ProtecaoDeDados => TipoDeGovernanca.ProtecaoDeDados,
        _ => throw new ArgumentOutOfRangeException(nameof(codigo), codigo, null),
    };
}
