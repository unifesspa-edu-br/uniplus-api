namespace Unifesspa.UniPlus.Contratos.UnitTests.SolutionRules;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using TestSupport;

public sealed class OpenApiReferenciasResolvidasTests
{
    [Fact(DisplayName = "Todo documento OpenAPI publicado resolve cada referência dentro do próprio documento")]
    public void Toda_ReferenciaDosDocumentosPublicadosApontaParaTrechoExistente()
    {
        string[] semResolucao =
        [
            .. OpenApiBaselines.Paths().SelectMany(static caminho =>
            {
                JsonNode documento = JsonNode.Parse(File.ReadAllText(caminho))!;
                return ReferenciasDe(documento)
                    .Where(referencia => !Existe(documento, referencia))
                    .Select(referencia => $"{Path.GetFileName(caminho)}: {referencia}");
            }),
        ];

        semResolucao.Should().BeEmpty("nenhum documento OpenAPI pode referenciar um trecho ausente");
    }

    private static bool Existe(JsonNode documento, string referencia) =>
        referencia.StartsWith("#/", StringComparison.Ordinal)
        && referencia[2..].Split('/').Select(DesfazerEscapeDoPonteiro).Aggregate((JsonNode?)documento, Descer) is not null;

    private static JsonNode? Descer(JsonNode? atual, string parte) =>
        atual is JsonObject objeto && objeto.TryGetPropertyValue(parte, out JsonNode? filho) ? filho : null;

    private static string DesfazerEscapeDoPonteiro(string parte) =>
        Uri.UnescapeDataString(parte).Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);

    private static IEnumerable<string> ReferenciasDe(JsonNode? no) => no switch
    {
        JsonObject objeto => objeto.SelectMany(static par =>
            par.Key == "$ref" && par.Value is JsonValue valor ? [valor.GetValue<string>()] : ReferenciasDe(par.Value)),
        JsonArray lista => lista.SelectMany(ReferenciasDe),
        _ => [],
    };
}
