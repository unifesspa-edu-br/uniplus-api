namespace Unifesspa.UniPlus.Contratos.UnitTests.TestSupport;

using System.IO;
using System.Text.Json;

using AwesomeAssertions;

internal static class ProblemDetailsPublicado
{
    private const string SchemaName = "ProblemDetails";

    internal static JsonElement Carregar(string baselinePath)
    {
        File.Exists(baselinePath).Should().BeTrue($"contrato esperado em {baselinePath} não foi encontrado.");

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(baselinePath));
        doc.RootElement.TryGetProperty("components", out JsonElement components).Should().BeTrue();
        components.TryGetProperty("schemas", out JsonElement schemas).Should().BeTrue();
        schemas.TryGetProperty(SchemaName, out JsonElement problemDetails).Should().BeTrue(
            $"{Path.GetFileName(baselinePath)} deveria publicar o schema {SchemaName} (ADR-0023).");

        return problemDetails.Clone();
    }
}
