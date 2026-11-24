namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>
/// Papel de governança que um perfil de sistema exerce. Só os perfis de governança
/// carregam um valor; os perfis comuns não têm nenhum.
/// </summary>
public enum TipoDeGovernanca
{
    /// <summary>Opera as concessões: concede, revoga, mantém os perfis e publica o termo de responsabilidade.</summary>
    GestorDeAcessos = 1,

    /// <summary>
    /// Decide, em nome da instituição, a segunda etapa dos pedidos com dado pessoal ou sensível, as versões protegidas,
    /// a revisão das concessões e a proteção do catálogo (ADR-0162).
    /// </summary>
    RepresentanteDoControlador = 2,

    /// <summary>
    /// Acompanha o tratamento de dados pessoais na gestão de acessos: consulta, registra manifestação técnica e solicita
    /// providências, sem aprovar, recusar nem revogar (ADR-0162).
    /// </summary>
    GestorDeProtecaoDeDados = 3,
}

/// <summary>Código textual canônico de <see cref="TipoDeGovernanca"/>, o valor gravado e exposto.</summary>
public static class TipoDeGovernancaCodigo
{
    public const string GestorDeAcessos = "GESTOR_DE_ACESSOS";
    public const string RepresentanteDoControlador = "REPRESENTANTE_DO_CONTROLADOR";
    public const string GestorDeProtecaoDeDados = "GESTOR_DE_PROTECAO_DE_DADOS";

    public static string ToCodigo(this TipoDeGovernanca tipo) => tipo switch
    {
        TipoDeGovernanca.GestorDeAcessos => GestorDeAcessos,
        TipoDeGovernanca.RepresentanteDoControlador => RepresentanteDoControlador,
        TipoDeGovernanca.GestorDeProtecaoDeDados => GestorDeProtecaoDeDados,
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, null),
    };

    public static TipoDeGovernanca FromCodigo(string codigo) => codigo switch
    {
        GestorDeAcessos => TipoDeGovernanca.GestorDeAcessos,
        RepresentanteDoControlador => TipoDeGovernanca.RepresentanteDoControlador,
        GestorDeProtecaoDeDados => TipoDeGovernanca.GestorDeProtecaoDeDados,
        _ => throw new ArgumentOutOfRangeException(nameof(codigo), codigo, null),
    };
}
