namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>Etapa da aprovação de uma versão de perfil.</summary>
public enum EtapaDaVersao
{
    /// <summary>A gestão de acessos, sempre a primeira.</summary>
    GestaoDeAcessos = 1,

    /// <summary>A proteção de dados, depois da gestão, quando o acréscimo é pessoal, identificador ou sensível.</summary>
    ProtecaoDeDados = 2,
}

/// <summary>Código textual canônico de <see cref="EtapaDaVersao"/>, o valor gravado e exposto.</summary>
public static class EtapaDaVersaoCodigo
{
    public const string GestaoDeAcessos = "gestao_de_acessos";
    public const string ProtecaoDeDados = "protecao_de_dados";

    public static string ToCodigo(this EtapaDaVersao etapa) => etapa switch
    {
        EtapaDaVersao.GestaoDeAcessos => GestaoDeAcessos,
        EtapaDaVersao.ProtecaoDeDados => ProtecaoDeDados,
        _ => throw new ArgumentOutOfRangeException(nameof(etapa), etapa, null),
    };

    public static EtapaDaVersao FromCodigo(string codigo) => codigo switch
    {
        GestaoDeAcessos => EtapaDaVersao.GestaoDeAcessos,
        ProtecaoDeDados => EtapaDaVersao.ProtecaoDeDados,
        _ => throw new ArgumentOutOfRangeException(nameof(codigo), codigo, null),
    };

    /// <summary>Lê o código informado; nulo quando não é de nenhuma etapa.</summary>
    public static EtapaDaVersao? TentarLer(string? codigo) => codigo switch
    {
        GestaoDeAcessos => EtapaDaVersao.GestaoDeAcessos,
        ProtecaoDeDados => EtapaDaVersao.ProtecaoDeDados,
        _ => null,
    };
}
