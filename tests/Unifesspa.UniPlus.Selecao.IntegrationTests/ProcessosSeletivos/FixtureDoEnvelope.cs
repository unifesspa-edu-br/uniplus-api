namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

/// <summary>
/// Localização e leitura das golden fixtures do envelope canônico.
/// </summary>
internal static class FixtureDoEnvelope
{
    /// <summary>
    /// Nome do arquivo da fixture — fixo, sem a schema_version. Enquanto cada versão tinha
    /// decoder próprio e congelado, o nome versionado guardava uma fixture por versão; com um
    /// codec único reescrito no lugar, só a forma corrente é lida, e o número no nome obrigava
    /// a renomear três arquivos a cada bump sem nada em troca. O par versão-forma continua
    /// verificado pelo teste que compara a versão declarada à do canonicalizador.
    /// </summary>
    private const string NomeDaFixture = "envelope.json";

    // A variante COM cascata (Story #575) é uma segunda fixture da MESMA versão de schema, não
    // uma versão nova: nome próprio, fora da chave por schema_version.
    private const string NomeDaFixtureCascata = "envelope-cascata.json";

    /// <summary>Versão de schema que as golden fixtures congelam, declarada ao lado delas.</summary>
    private const string NomeDaVersaoDeclarada = "schema-version.txt";

    internal static string? LerFixture() => LerSeExistir(CaminhoDaFixture());

    internal static string? LerFixtureCascata() => LerSeExistir(CaminhoDaFixtureCascata());

    internal static string CaminhoDaVersaoDeclarada() => CaminhoNoBinario(NomeDaVersaoDeclarada);

    private static string? LerSeExistir(string caminho) => File.Exists(caminho) ? File.ReadAllText(caminho).Trim() : null;

    private static string CaminhoDaFixture() => CaminhoNoBinario(NomeDaFixture);

    private static string CaminhoDaFixtureCascata() => CaminhoNoBinario(NomeDaFixtureCascata);

    private static string CaminhoNoBinario(string nome) => Path.Join(
        AppContext.BaseDirectory,
        "ProcessosSeletivos",
        "Fixtures",
        nome);

}
