namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>A etapa em que a decisão sobre uma concessão da janela foi tomada.</summary>
public enum EtapaDaMigracao
{
    /// <summary>A gestão de acessos, sempre a primeira.</summary>
    GestaoDeAcessos = 1,

    /// <summary>A proteção de dados, depois da gestão, quando o acréscimo é pessoal, identificador ou sensível.</summary>
    ProtecaoDeDados = 2,

    /// <summary>O sistema, que retém a concessão do gestor e encerra a que ninguém decidiu.</summary>
    Sistema = 3,
}

/// <summary>Código textual canônico de <see cref="EtapaDaMigracao"/>, o valor gravado e exposto.</summary>
public static class EtapaDaMigracaoCodigo
{
    public const string GestaoDeAcessos = "gestao_de_acessos";
    public const string ProtecaoDeDados = "protecao_de_dados";
    public const string Sistema = "sistema";

    public static string ToCodigo(this EtapaDaMigracao etapa) => etapa switch
    {
        EtapaDaMigracao.GestaoDeAcessos => GestaoDeAcessos,
        EtapaDaMigracao.ProtecaoDeDados => ProtecaoDeDados,
        EtapaDaMigracao.Sistema => Sistema,
        _ => throw new ArgumentOutOfRangeException(nameof(etapa), etapa, null),
    };

    public static EtapaDaMigracao FromCodigo(string codigo) => codigo switch
    {
        GestaoDeAcessos => EtapaDaMigracao.GestaoDeAcessos,
        ProtecaoDeDados => EtapaDaMigracao.ProtecaoDeDados,
        Sistema => EtapaDaMigracao.Sistema,
        _ => throw new ArgumentOutOfRangeException(nameof(codigo), codigo, null),
    };
}
