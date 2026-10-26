namespace Unifesspa.UniPlus.Selecao.ArchTests;

using System.Runtime.CompilerServices;

using AwesomeAssertions;

/// <summary>
/// O cruzamento das notas com as inscrições e a avaliação não conhecem nenhum leiaute de arquivo
/// (ADR-0148): só quem lê ou escreve o arquivo do INEP referencia o namespace dos leiautes. Um leiaute
/// novo entra sem tocar o que vem depois da leitura.
/// </summary>
public sealed class LeiautesDoInepIsoladosTests
{
    private static readonly string[] CaminhosPermitidos =
    [
        Path.Join("Unifesspa.UniPlus.Selecao.Domain", "Leiautes"),
        Path.Join("Unifesspa.UniPlus.Selecao.Application", "Commands", "NotasDoEnem"),
        Path.Join("Unifesspa.UniPlus.Selecao.Application", "Queries", "NotasDoEnem"),
        Path.Join("Unifesspa.UniPlus.Selecao.Application", "DTOs", "NotasDoEnem"),
        Path.Join("Unifesspa.UniPlus.Selecao.API", "Controllers", "NotasDoEnemController.cs"),
    ];

    [Fact(DisplayName = "Só a leitura e a escrita do arquivo do INEP referenciam o namespace dos leiautes")]
    public void SoOArquivoReferenciaOsLeiautes()
    {
        string[] referencias = [.. Directory
            .EnumerateFiles(Raiz("src", "selecao"), "*.cs", SearchOption.AllDirectories)
            .Where(static arquivo => !EhGerado(arquivo))
            .Where(static arquivo => File.ReadAllText(arquivo).Contains("Selecao.Domain.Leiautes", StringComparison.Ordinal)
                || File.ReadAllText(arquivo).Contains("Domain.Leiautes;", StringComparison.Ordinal))
            .Where(static arquivo => !CaminhosPermitidos.Any(permitido => arquivo.Contains(permitido, StringComparison.Ordinal)))
            .Select(static arquivo => Path.GetFileName(arquivo))
            .Order(StringComparer.Ordinal)];

        referencias.Should().BeEmpty(
            "o cruzamento, as entidades e a persistência trabalham com as linhas lidas, nunca com o leiaute");
    }

    private static bool EhGerado(string arquivo) =>
        arquivo.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || arquivo.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static string Raiz(string primeiro, params string[] resto) =>
        Path.GetFullPath(Path.Join([RaizDoRepositorio(), primeiro, .. resto]));

    private static string RaizDoRepositorio([CallerFilePath] string origem = "") =>
        Path.Join(Path.GetDirectoryName(origem)!, "..", "..");
}
