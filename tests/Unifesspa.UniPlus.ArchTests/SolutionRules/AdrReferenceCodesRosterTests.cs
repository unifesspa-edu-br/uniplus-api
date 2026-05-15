namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.IO;
using System.Text.Json;

using AwesomeAssertions;

using TestSupport;

/// <summary>
/// Fitness test <strong>R9</strong> da ADR-0055: para cada linha em
/// <c>seeds/seed-areas-organizacionais.json</c>, o <c>adrReferenceCode</c>
/// referencia arquivo existente em <c>docs/adrs/</c>.
/// </summary>
/// <remarks>
/// Escopo mínimo deliberado: validação de formato (regex NNNN-slug) e shape
/// das entradas já é responsabilidade do Domain factory
/// <c>AreaOrganizacional.Criar</c>; aqui só travamos o drift que importa para
/// auditoria — área no seed sem ADR commitada nesta base.
/// </remarks>
public sealed class AdrReferenceCodesRosterTests
{
    private const string SeedRelativePath = "seeds/seed-areas-organizacionais.json";
    private const string AdrsRelativePath = "docs/adrs";

    [Fact(DisplayName = "R9: cada AdrReferenceCode no seed referencia arquivo existente em docs/adrs/")]
    public void AdrReferenceCodes_DevemReferenciarArquivoExistente()
    {
        string solutionRoot = SolutionRootLocator.Locate();
        string seedPath = Path.Combine(solutionRoot, SeedRelativePath);
        string adrsDir = Path.Combine(solutionRoot, AdrsRelativePath);

        File.Exists(seedPath).Should().BeTrue(
            $"ADR-0055 §\"Invariante de roster fechado\" exige {SeedRelativePath} versionado.");
        Directory.Exists(adrsDir).Should().BeTrue(
            $"diretório {AdrsRelativePath} obrigatório — ADRs são fonte de verdade do roster fechado.");

        JsonDocument doc = JsonDocument.Parse(File.ReadAllText(seedPath));
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);

        List<string> orfaos = [];
        foreach (JsonElement entry in doc.RootElement.EnumerateArray())
        {
            string? adrRef = entry.TryGetProperty("adrReferenceCode", out JsonElement el)
                ? el.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(adrRef))
            {
                orfaos.Add("<linha sem adrReferenceCode>");
                continue;
            }

            string adrFilePath = Path.Combine(adrsDir, $"{adrRef}.md");
            if (!File.Exists(adrFilePath))
            {
                orfaos.Add(adrRef);
            }
        }

        orfaos.Should().BeEmpty(
            "Closed roster ADR-0055: cada área no seed precisa de ADR correspondente "
            + $"em docs/adrs/. Faltam: {string.Join(", ", orfaos)}");
    }
}
