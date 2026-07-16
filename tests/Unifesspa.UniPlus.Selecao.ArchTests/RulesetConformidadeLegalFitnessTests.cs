namespace Unifesspa.UniPlus.Selecao.ArchTests;

using System.Runtime.CompilerServices;

using AwesomeAssertions;

/// <summary>
/// Fitness test do contrato temporal da ADR-0114: a seleção jurídica recebe a
/// data do chamador e não pode trocar esse fato por uma leitura de relógio.
/// </summary>
public sealed class RulesetConformidadeLegalFitnessTests
{
    private static readonly string[] LiteraisDeRelogioProibidos =
    [
        "TimeProvider",
        "DateTimeOffset.UtcNow",
        "DateTimeOffset.Now",
        "DateTime.UtcNow",
        "DateTime.Now",
    ];

    [Fact(DisplayName = "Ruleset_NaoLeRelogio — handler recebe DataReferencia explicitamente")]
    public void Ruleset_NaoLeRelogio()
    {
        string fonte = File.ReadAllText(CaminhoHandler());

        fonte.Should().Contain("query.DataReferencia");
        foreach (string literal in LiteraisDeRelogioProibidos)
        {
            fonte.Should().NotContain(literal);
        }
    }

    [Fact(DisplayName = "Ruleset_NaoLeRelogio — repositório recebe a data como parâmetro, nunca lê o relógio")]
    public void Ruleset_RepositorioNaoLeRelogio()
    {
        // O arquivo tem outro método (listagem admin, filtro `vigentes=true`) que
        // legitimamente lê o relógio para "o que está ativo agora" — a ADR-0114
        // declara esse caminho fora do escopo do gate legal. O fitness isola só o
        // corpo de ObterVigentesParaTipoProcessoAsync, não o arquivo inteiro.
        string metodo = ExtrairCorpoDoMetodo(
            File.ReadAllText(CaminhoRepositorio()), "ObterVigentesParaTipoProcessoAsync");

        metodo.Should().Contain("dataReferencia");
        foreach (string literal in LiteraisDeRelogioProibidos)
        {
            metodo.Should().NotContain(literal);
        }
    }

    private static string ExtrairCorpoDoMetodo(string fonte, string nomeDoMetodo)
    {
        int inicio = fonte.IndexOf(nomeDoMetodo, StringComparison.Ordinal);
        inicio.Should().BeGreaterThanOrEqualTo(0, $"o método {nomeDoMetodo} deveria existir no arquivo");

        int proximoMetodo = fonte.IndexOf("\n    public ", inicio + nomeDoMetodo.Length, StringComparison.Ordinal);
        int fim = proximoMetodo >= 0 ? proximoMetodo : fonte.Length;
        return fonte[inicio..fim];
    }

    private static string CaminhoHandler([CallerFilePath] string origem = "") =>
        Path.GetFullPath(Path.Join(
            Path.GetDirectoryName(origem)!,
            "..",
            "..",
            "src",
            "selecao",
            "Unifesspa.UniPlus.Selecao.Application",
            "Queries",
            "ObrigatoriedadesLegais",
            "ObterObrigatoriedadesAplicaveisQueryHandler.cs"));

    private static string CaminhoRepositorio([CallerFilePath] string origem = "") =>
        Path.GetFullPath(Path.Join(
            Path.GetDirectoryName(origem)!,
            "..",
            "..",
            "src",
            "selecao",
            "Unifesspa.UniPlus.Selecao.Infrastructure",
            "Persistence",
            "Repositories",
            "ObrigatoriedadeLegalRepository.cs"));
}
