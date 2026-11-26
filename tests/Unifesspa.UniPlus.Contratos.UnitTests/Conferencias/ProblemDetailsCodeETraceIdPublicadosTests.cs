namespace Unifesspa.UniPlus.Contratos.UnitTests.SolutionRules;

using System.IO;
using System.Text.Json;

using AwesomeAssertions;

using TestSupport;

/// <summary>
/// A ADR-0023 torna <c>code</c> e <c>traceId</c> obrigatórios em toda resposta de erro.
/// Os dois viajam por <c>ProblemDetails.Extensions</c>, que o gerador OpenAPI descreve como
/// <c>additionalProperties</c>: sem declaração no contrato, quem gera cliente tipado fica sem
/// propriedade para o código do erro e para o identificador do pedido.
/// Esta função de aptidão guarda a declaração em todos os contratos publicados.
/// </summary>
public sealed class ProblemDetailsCodeETraceIdPublicadosTests
{
    private const string CodePattern = @"^[a-z]+(\.[a-z_]+)+$";
    private const string TraceIdPattern = "^[0-9a-f]{32}$";

    [Fact(DisplayName = "ADR-0023: os contratos publicados declaram `code` obrigatório e com o padrão da taxonomia")]
    public void ProblemDetails_DeclaraCodeObrigatorioComPadrao_EmTodosOsBaselines() =>
        ConferirEmTodosOsContratos("code", CodePattern);

    [Fact(DisplayName = "ADR-0023: os contratos publicados declaram `traceId` obrigatório com 32 caracteres hexadecimais")]
    public void ProblemDetails_DeclaraTraceIdObrigatorioComPadrao_EmTodosOsBaselines() =>
        ConferirEmTodosOsContratos("traceId", TraceIdPattern);

    private static void ConferirEmTodosOsContratos(string propriedade, string padraoEsperado)
    {
        string[] ausencias = [.. OpenApiBaselines.Paths().SelectMany(caminho => ConferirContrato(caminho, propriedade, padraoEsperado))];

        ausencias.Should().BeEmpty(
            because: "sem a declaração no contrato o campo existe na resposta e não existe no contrato publicado — "
                + "rode `dotnet test "
                + "tests/Unifesspa.UniPlus.<modulo>.IntegrationTests --filter \"FullyQualifiedName~SpecRuntime\"` "
                + "no módulo desatualizado e confira as diferenças em contracts/.");
    }

    private static IEnumerable<string> ConferirContrato(string caminho, string propriedade, string padraoEsperado)
    {
        string arquivo = Path.GetFileName(caminho);
        JsonElement problema = ProblemDetailsPublicado.Carregar(caminho);
        if (!problema.TryGetProperty("properties", out JsonElement propriedades)
            || !propriedades.TryGetProperty(propriedade, out JsonElement campo))
        {
            return [$"{arquivo}: ProblemDetails não declara `{propriedade}`."];
        }

        return [.. ConferirForma(arquivo, propriedade, campo, padraoEsperado), .. ConferirObrigatoriedade(arquivo, propriedade, problema)];
    }

    private static IEnumerable<string> ConferirForma(string arquivo, string propriedade, JsonElement campo, string padraoEsperado)
    {
        if (TextoDe(campo, "type") != "string")
            yield return $"{arquivo}: `{propriedade}` deveria ser texto.";
        if (string.IsNullOrWhiteSpace(TextoDe(campo, "description")))
            yield return $"{arquivo}: `{propriedade}` sem descrição.";
        if (TextoDe(campo, "pattern") != padraoEsperado)
            yield return $"{arquivo}: `{propriedade}` deveria declarar o padrão `{padraoEsperado}`.";
    }

    private static IEnumerable<string> ConferirObrigatoriedade(string arquivo, string propriedade, JsonElement problema)
    {
        bool obrigatorio = problema.TryGetProperty("required", out JsonElement obrigatorios)
            && obrigatorios.ValueKind == JsonValueKind.Array
            && obrigatorios.EnumerateArray().Any(item => item.GetString() == propriedade);

        if (!obrigatorio)
            yield return $"{arquivo}: `{propriedade}` deveria constar em `required`.";
    }

    private static string? TextoDe(JsonElement objeto, string nome) =>
        objeto.TryGetProperty(nome, out JsonElement valor) && valor.ValueKind == JsonValueKind.String
            ? valor.GetString()
            : null;
}
