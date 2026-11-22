namespace Unifesspa.UniPlus.Contratos.UnitTests.SolutionRules;

using System.IO;
using System.Text.Json;

using AwesomeAssertions;

using TestSupport;

/// <summary>
/// A ADR-0134 distingue o conflito que vale repetir daquele que só muda de
/// estado por outra ação, e declara que essa distinção fica pública na resposta
/// <b>e no contrato</b>, para que cliente gerado a enxergue como propriedade tipada.
/// <para>
/// Sair na resposta não basta para ser público. O campo viaja por
/// <c>ProblemDetails.Extensions</c>, que o gerador OpenAPI descreve como
/// <c>additionalProperties</c> — quem gera cliente tipado a partir do baseline
/// fica sem propriedade para consultar, e a promessa da decisão não se cumpre.
/// Esta função de aptidão guarda a declaração nos cinco contratos publicados.
/// </para>
/// </summary>
public sealed class ProblemDetailsRetryablePublicadoTests
{
    private const string SchemaName = "ProblemDetails";
    private const string PropertyName = "retryable";

    [Fact(DisplayName = "ADR-0134: os contratos publicados declaram `retryable` em ProblemDetails")]
    public void ProblemDetails_Declara_Retryable_Em_Todos_Os_Baselines()
    {
        string[] ausencias = [.. OpenApiBaselines.Paths().SelectMany(ConferirContrato)];

        ausencias.Should().BeEmpty(
            because: "sem a declaração no baseline o sinal existe na resposta e não existe no contrato — "
                + "regerar com `UPDATE_OPENAPI_BASELINE=1 dotnet test "
                + "tests/Unifesspa.UniPlus.<modulo>.IntegrationTests --filter \"FullyQualifiedName~SpecRuntime\"` "
                + "no módulo stale e conferir o diff em contracts/.");
    }

    private static IEnumerable<string> ConferirContrato(string caminho)
    {
        string arquivo = Path.GetFileName(caminho);
        JsonElement problema = LoadProblemDetails(caminho);
        if (!problema.TryGetProperty("properties", out JsonElement propriedades)
            || !propriedades.TryGetProperty(PropertyName, out JsonElement campo))
        {
            return [$"{arquivo}: ProblemDetails não declara `{PropertyName}`."];
        }

        return ConferirPropriedade(arquivo, problema, campo);
    }

    private static IEnumerable<string> ConferirPropriedade(string arquivo, JsonElement problema, JsonElement campo)
    {
        if (!campo.TryGetProperty("type", out JsonElement tipo) || tipo.ValueKind != JsonValueKind.String || tipo.GetString() != "boolean")
            yield return $"{arquivo}: `{PropertyName}` deveria ser boolean.";
        if (!campo.TryGetProperty("description", out JsonElement descricao) || string.IsNullOrWhiteSpace(descricao.GetString()))
            yield return $"{arquivo}: `{PropertyName}` sem descrição.";
        if (problema.TryGetProperty("required", out JsonElement obrigatorios) && obrigatorios.ValueKind == JsonValueKind.Array
            && obrigatorios.EnumerateArray().Any(item => item.GetString() == PropertyName))
            yield return $"{arquivo}: `{PropertyName}` não pode ser obrigatório.";
    }

    private static JsonElement LoadProblemDetails(string baselinePath)
    {
        File.Exists(baselinePath).Should().BeTrue($"baseline esperado em {baselinePath} não foi encontrado.");

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(baselinePath));
        doc.RootElement.TryGetProperty("components", out JsonElement components).Should().BeTrue();
        components.TryGetProperty("schemas", out JsonElement schemas).Should().BeTrue();
        schemas.TryGetProperty(SchemaName, out JsonElement problemDetails).Should().BeTrue(
            $"{Path.GetFileName(baselinePath)} deveria publicar o schema {SchemaName} (ADR-0023).");

        return problemDetails.Clone();
    }
}
