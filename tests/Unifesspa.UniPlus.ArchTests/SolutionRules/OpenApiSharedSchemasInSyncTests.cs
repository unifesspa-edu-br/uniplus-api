namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.IO;
using System.Text.Json;

using AwesomeAssertions;

using TestSupport;

/// <summary>
/// Fitness function da ADR-0035: schemas duplicados entre baselines OpenAPI
/// dos módulos (<c>contracts/openapi.{selecao,ingresso}.json</c>) devem ser
/// byte-equivalentes. <c>Microsoft.AspNetCore.OpenApi</c> 10 emite cada
/// documento independentemente — então é possível regerar um baseline e
/// esquecer o outro, deixando o contrato cross-module inconsistente sem
/// alarme do drift check por módulo.
/// </summary>
/// <remarks>
/// Schemas vindos do shared kernel (<c>ProblemDetails</c>,
/// <c>AuthenticatedUserResponse</c>, <c>UserProfileResponse</c>) são os
/// candidatos óbvios. A regra captura QUALQUER schema com mesmo nome em
/// ambos os documentos — extensível por construção: se o time mover mais
/// tipos para shared, eles passam automaticamente a ser fitness-checked.
///
/// Quando este teste falhar:
/// <list type="number">
///   <item>Identificar qual schema divergiu (mensagem do teste).</item>
///   <item>Regerar o baseline do módulo stale:
///     <c>UPDATE_OPENAPI_BASELINE=1 dotnet test tests/Unifesspa.UniPlus.{Modulo}.IntegrationTests --filter "FullyQualifiedName~SpecRuntime"</c>.</item>
///   <item>Confirmar via <c>git diff contracts/</c> que a mudança é intencional.</item>
/// </list>
/// </remarks>
public sealed class OpenApiSharedSchemasInSyncTests
{
    private static readonly string[] BaselinePaths =
    [
        Path.Combine("contracts", "openapi.selecao.json"),
        Path.Combine("contracts", "openapi.ingresso.json"),
    ];

    private static readonly JsonSerializerOptions CanonicalOptions = new()
    {
        WriteIndented = true,
    };

    [Fact(DisplayName = "ADR-0035: schemas com mesmo nome em baselines diferentes são byte-equivalentes")]
    public void Schemas_Compartilhados_Devem_Ser_ByteEquivalentes()
    {
        string solutionRoot = SolutionRootLocator.Locate();
        Dictionary<string, JsonElement>[] schemasByModule =
        [
            LoadSchemas(Path.Combine(solutionRoot, BaselinePaths[0])),
            LoadSchemas(Path.Combine(solutionRoot, BaselinePaths[1])),
        ];

        IEnumerable<string> sharedNames = schemasByModule[0].Keys
            .Intersect(schemasByModule[1].Keys, StringComparer.Ordinal)
            .OrderBy(static n => n, StringComparer.Ordinal);

        sharedNames.Should().NotBeEmpty(
            "ao menos `ProblemDetails` é compartilhado entre os módulos via ADR-0023; ausência aqui sugere regressão da geração OpenAPI.");

        List<string> divergencias = [];

        foreach (string name in sharedNames)
        {
            string canonicalSelecao = SerializeCanonical(schemasByModule[0][name]);
            string canonicalIngresso = SerializeCanonical(schemasByModule[1][name]);

            if (!string.Equals(canonicalSelecao, canonicalIngresso, StringComparison.Ordinal))
            {
                divergencias.Add(
                    $"Schema '{name}' diverge entre selecao e ingresso. "
                    + "Para sincronizar, rode "
                    + "`UPDATE_OPENAPI_BASELINE=1 dotnet test tests/Unifesspa.UniPlus.{modulo}.IntegrationTests --filter \"FullyQualifiedName~SpecRuntime\"` "
                    + "no módulo stale e confira o diff em contracts/.");
            }
        }

        divergencias.Should().BeEmpty(
            because: "schemas duplicados entre baselines violam o contrato cross-module (ADR-0035).");
    }

    private static Dictionary<string, JsonElement> LoadSchemas(string baselinePath)
    {
        File.Exists(baselinePath).Should().BeTrue($"baseline esperado em {baselinePath} não foi encontrado.");

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(baselinePath));
        if (!doc.RootElement.TryGetProperty("components", out JsonElement components)
            || !components.TryGetProperty("schemas", out JsonElement schemas))
        {
            return new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        }

        Dictionary<string, JsonElement> map = new(StringComparer.Ordinal);
        foreach (JsonProperty schema in schemas.EnumerateObject())
        {
            // JsonElement.Clone() retorna uma cópia independente do JsonDocument
            // de origem (docs Microsoft: "can be safely stored beyond the lifetime
            // of the original JsonDocument") — seguro após o using-dispose.
            map[schema.Name] = schema.Value.Clone();
        }
        return map;
    }

    private static string SerializeCanonical(JsonElement schema)
    {
        // Re-serializa com WriteIndented + canonical encoder default — o
        // mesmo path usado por OpenApiEndpointTests.NormalizeJson, garantindo
        // diff estável independente de espaços e ordering preservada do
        // documento de origem (System.Text.Json mantém ordem de inserção).
        return JsonSerializer.Serialize(schema, CanonicalOptions);
    }

}
