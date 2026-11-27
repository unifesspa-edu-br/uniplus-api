namespace Unifesspa.UniPlus.Contratos.UnitTests.SolutionRules;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using TestSupport;

public sealed class CorpoAcimaDoLimiteDeclaradoTests
{
    [Fact(DisplayName = "Toda operação publicada com corpo declara o 413 do limite, em application/problem+json")]
    public void Toda_OperacaoComCorpoDeclara413DoLimite()
    {
        string[] semDeclaracao =
        [
            .. OpenApiBaselines.Paths().SelectMany(static caminho =>
                OperacoesComCorpo(JsonNode.Parse(File.ReadAllText(caminho))!)
                    .Where(static operacao => !Declara413(operacao.Value))
                    .Select(operacao => $"{Path.GetFileName(caminho)}: {operacao.Key}")),
        ];

        semDeclaracao.Should().BeEmpty("o limite do corpo vale para toda operação que recebe corpo");
    }

    [Fact(DisplayName = "Os documentos publicados têm ao menos uma operação com corpo")]
    public void DocumentosPublicados_TemOperacaoComCorpo()
    {
        OpenApiBaselines.Paths()
            .SelectMany(static caminho => OperacoesComCorpo(JsonNode.Parse(File.ReadAllText(caminho))!))
            .Should().NotBeEmpty("sem operação com corpo a conferência não confere nada");
    }

    private static IEnumerable<KeyValuePair<string, JsonObject>> OperacoesComCorpo(JsonNode documento) =>
        documento["paths"]!.AsObject().SelectMany(static caminho => caminho.Value!.AsObject()
            .Where(static verbo => verbo.Value is JsonObject operacao && operacao.ContainsKey("requestBody"))
            .Select(verbo => KeyValuePair.Create($"{verbo.Key.ToUpperInvariant()} {caminho.Key}", verbo.Value!.AsObject())));

    private static bool Declara413(JsonObject operacao) =>
        operacao["responses"]?["413"]?["content"]?["application/problem+json"] is not null;
}
