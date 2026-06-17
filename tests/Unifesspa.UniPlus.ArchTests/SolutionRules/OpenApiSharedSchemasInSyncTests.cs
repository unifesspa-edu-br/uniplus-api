namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.IO;
using System.Text.Json;

using AwesomeAssertions;

using TestSupport;

/// <summary>
/// Fitness function da ADR-0035: schemas duplicados entre baselines OpenAPI
/// dos módulos (<c>contracts/openapi.{selecao,ingresso,organizacao}.json</c>) devem ser
/// byte-equivalentes. <c>Microsoft.AspNetCore.OpenApi</c> 10 emite cada
/// documento independentemente — então é possível regerar um baseline e
/// esquecer o outro, deixando o contrato cross-module inconsistente sem
/// alarme do drift check por módulo.
/// </summary>
/// <remarks>
/// Schemas vindos do shared kernel (<c>ProblemDetails</c>,
/// <c>AuthenticatedUserResponse</c>, <c>UserProfileResponse</c>) são os
/// candidatos óbvios. A regra captura QUALQUER schema com mesmo nome em
/// dois ou mais documentos — extensível por construção: se o time mover mais
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
    private const string ContractsDir = "contracts";

    private static readonly string[] BaselineFileNames =
    [
        "openapi.selecao.json",
        "openapi.ingresso.json",
        "openapi.organizacao.json",
        "openapi.geo.json",
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
            [.. BaselineFileNames.Select(f => LoadSchemas(Path.Combine(solutionRoot, ContractsDir, f)))];

        // Pairwise, NÃO interseção de todos: um schema compartilhado por
        // QUALQUER par de módulos deve ser byte-equivalente, mesmo que ausente
        // num terceiro. Interseção total deixaria escapar um DTO usado só por,
        // p.ex., Seleção e Ingresso (ausente em Organização) — enfraquecendo a
        // guarda da ADR-0035. Critério: nome presente em ≥2 módulos.
        IReadOnlyList<string> sharedNames = schemasByModule
            .SelectMany(static schemas => schemas.Keys)
            .GroupBy(static name => name, StringComparer.Ordinal)
            .Where(static grupo => grupo.Count() > 1)
            .Select(static grupo => grupo.Key)
            .OrderBy(static n => n, StringComparer.Ordinal)
            .ToList();

        sharedNames.Should().NotBeEmpty(
            "ao menos `ProblemDetails` é compartilhado entre os módulos via ADR-0023; ausência aqui sugere regressão da geração OpenAPI.");

        List<string> divergencias = [];

        foreach (string name in sharedNames)
        {
            // Compara TODAS as ocorrências do schema (nos módulos que o têm):
            // mais de uma forma canônica distinta = divergência.
            bool divergiu = schemasByModule
                .Where(schemas => schemas.ContainsKey(name))
                .Select(schemas => SerializeCanonical(schemas[name]))
                .Distinct(StringComparer.Ordinal)
                .Count() > 1;

            if (divergiu)
            {
                divergencias.Add(
                    $"Schema '{name}' diverge entre baselines OpenAPI. "
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
