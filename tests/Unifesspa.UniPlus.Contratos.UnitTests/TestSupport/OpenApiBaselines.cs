namespace Unifesspa.UniPlus.Contratos.UnitTests.TestSupport;

using AwesomeAssertions;

internal static class OpenApiBaselines
{
    internal static IReadOnlyList<string> Paths()
    {
        string pasta = Path.Join(AppContext.BaseDirectory, "contracts");
        string[] caminhos = Directory.GetFiles(pasta, "openapi.*.json").Order(StringComparer.Ordinal).ToArray();
        caminhos.Should().NotBeEmpty("os contratos OpenAPI devem ser copiados para a saída");
        return caminhos;
    }
}
