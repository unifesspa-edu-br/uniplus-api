namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Reflection;
using System.Runtime.CompilerServices;

/// <summary>
/// Localização e leitura das golden fixtures do envelope canônico, e a regeneração explícita
/// delas.
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

    internal static string LerFixture() => File.ReadAllText(CaminhoDaFixture()).Trim();

    internal static string LerFixtureCascata() => File.ReadAllText(CaminhoDaFixtureCascata()).Trim();

    internal static string CaminhoDaVersaoDeclarada() => CaminhoNoBinario(NomeDaVersaoDeclarada);

    /// <summary>Caminho da fixture na ÁRVORE-FONTE — só usado na regeneração explícita.</summary>
    internal static string CaminhoDaFixtureNoFonte([CallerFilePath] string origem = "") => CaminhoNaArvoreFonte(origem, NomeDaFixture);

    internal static string CaminhoDaFixtureCascataNoFonte([CallerFilePath] string origem = "") => CaminhoNaArvoreFonte(origem, NomeDaFixtureCascata);

    /// <summary>
    /// Regeneração explícita, no mesmo espírito de UPDATE_OPENAPI_BASELINE:
    ///   UPDATE_ENVELOPE_FIXTURE=1 dotnet test --filter Envelope_BateGoldenFixture
    /// e o diff da fixture entra no PR — a mudança do envelope passa a ser visível na revisão,
    /// que é todo o ponto.
    /// </summary>
    internal static void RegenerarSeSolicitado(string atual, string destino)
    {
        if (Environment.GetEnvironmentVariable("UPDATE_ENVELOPE_FIXTURE") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destino)!);
            File.WriteAllText(destino, atual + Environment.NewLine);
        }
    }

    private static string CaminhoDaFixture() => CaminhoNoBinario(NomeDaFixture);

    private static string CaminhoDaFixtureCascata() => CaminhoNoBinario(NomeDaFixtureCascata);

    private static string CaminhoNoBinario(string nome) => Path.Combine(
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
        "ProcessosSeletivos",
        "Fixtures",
        nome);

    private static string CaminhoNaArvoreFonte(string origem, string nome) => Path.Combine(
        Path.GetDirectoryName(origem)!,
        "Fixtures",
        nome);
}
