namespace Unifesspa.UniPlus.ArchTests.TestSupport;

/// <summary>
/// Os módulos de negócio da solução e a raiz de namespace de cada um. Módulo novo entra aqui, e as travas de fronteira
/// o passam a cobrir.
/// </summary>
internal static class ModulosDoMonolito
{
    internal static readonly string[] Nomes =
    [
        "Selecao",
        "Ingresso",
        "Portal",
        "OrganizacaoInstitucional",
        "Configuracao",
        "Publicacoes",
        "Discentes",
        "Autorizacao",
    ];

    internal static readonly string[] Raizes = [.. Nomes.Select(static n => $"Unifesspa.UniPlus.{n}")];

    /// <summary>Os dois módulos sintéticos que servem de contraprova às regras de fronteira.</summary>
    internal static readonly string[] RaizesDosFixtures =
    [
        "Unifesspa.UniPlus.ArchTests.Fixtures.Modulos.Alfa",
        "Unifesspa.UniPlus.ArchTests.Fixtures.Modulos.Beta",
    ];
}
