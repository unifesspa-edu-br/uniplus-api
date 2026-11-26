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
/// Esta função de aptidão guarda a declaração nos contratos publicados.
/// </para>
/// </summary>
public sealed class ProblemDetailsRetryablePublicadoTests
{
    private const string PropertyName = "retryable";

    [Fact(DisplayName = "ADR-0134: os contratos publicados declaram `retryable` em ProblemDetails")]
    public void ProblemDetails_Declara_Retryable_Em_Todos_Os_Baselines()
    {
        string[] ausencias = [.. OpenApiBaselines.Paths().SelectMany(ConferirContrato)];

        ausencias.Should().BeEmpty(
            because: "sem a declaração no baseline o sinal existe na resposta e não existe no contrato — "
                + "conferir com `dotnet test "
                + "tests/Unifesspa.UniPlus.<modulo>.IntegrationTests --filter \"FullyQualifiedName~SpecRuntime\"` "
                + "no módulo stale e conferir o diff em contracts/.");
    }

    private static IEnumerable<string> ConferirContrato(string caminho)
    {
        string arquivo = Path.GetFileName(caminho);
        JsonElement problema = ProblemDetailsPublicado.Carregar(caminho);
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
}
