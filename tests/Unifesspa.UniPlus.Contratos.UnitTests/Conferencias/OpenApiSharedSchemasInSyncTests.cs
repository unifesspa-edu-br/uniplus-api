namespace Unifesspa.UniPlus.Contratos.UnitTests.SolutionRules;

using System.IO;
using System.Text.Json;

using AwesomeAssertions;

using TestSupport;

public sealed class OpenApiSharedSchemasInSyncTests
{
    private static readonly JsonSerializerOptions CanonicalOptions = new()
    {
        WriteIndented = true,
    };

    [Fact(DisplayName = "ADR-0035: schemas com mesmo nome em baselines diferentes são byte-equivalentes")]
    public void Schemas_Compartilhados_Devem_Ser_ByteEquivalentes()
    {
        Dictionary<string, JsonElement>[] schemasByModule =
            [.. OpenApiBaselines.Paths().Select(LoadSchemas)];

        string[] sharedNames = NomesCompartilhados(schemasByModule);
        sharedNames.Should().NotBeEmpty("ProblemDetails é compartilhado entre os módulos pela ADR-0023");
        string[] divergencias = [.. sharedNames.Where(name => Diverge(name, schemasByModule))];

        divergencias.Should().BeEmpty(
            because: "schemas duplicados entre baselines violam o contrato cross-module (ADR-0035).");
    }

    private static string[] NomesCompartilhados(Dictionary<string, JsonElement>[] schemas) =>
        [.. schemas.SelectMany(static modulo => modulo.Keys).GroupBy(static nome => nome, StringComparer.Ordinal)
            .Where(static grupo => grupo.Count() > 1).Select(static grupo => grupo.Key).Order(StringComparer.Ordinal)];

    private static bool Diverge(string nome, Dictionary<string, JsonElement>[] schemas) =>
        schemas.Where(modulo => modulo.ContainsKey(nome)).Select(modulo => SerializeCanonical(modulo[nome]))
            .Distinct(StringComparer.Ordinal).Skip(1).Any();

    private static Dictionary<string, JsonElement> LoadSchemas(string baselinePath)
    {
        File.Exists(baselinePath).Should().BeTrue($"baseline esperado em {baselinePath} não foi encontrado.");

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(baselinePath));
        if (!doc.RootElement.TryGetProperty("components", out JsonElement components)
            || !components.TryGetProperty("schemas", out JsonElement schemas))
        {
            return new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        }

        return schemas.EnumerateObject().ToDictionary(static schema => schema.Name, static schema => schema.Value.Clone(), StringComparer.Ordinal);
    }

    private static string SerializeCanonical(JsonElement schema) => JsonSerializer.Serialize(schema, CanonicalOptions);
}
