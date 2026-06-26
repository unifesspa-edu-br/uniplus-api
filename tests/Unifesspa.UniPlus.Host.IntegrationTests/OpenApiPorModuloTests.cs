namespace Unifesspa.UniPlus.Host.IntegrationTests;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// Prova de P6 (OpenAPI por-módulo no host): no monólito co-hospedado, cada
/// documento <c>/openapi/{modulo}.json</c> deve conter apenas os endpoints do
/// seu módulo (sob <c>api/{modulo}/</c>) — sem vazar paths de outros módulos.
/// </summary>
/// <remarks>
/// Sem filtragem por documento, o Microsoft.AspNetCore.OpenApi inclui em cada
/// doc todo endpoint com <c>GroupName == null</c> — no processo único isso faria
/// o doc <c>configuracao</c> listar também <c>api/organizacao/*</c> etc.,
/// invalidando os baselines `contracts/openapi.*.json`. O <c>GroupName</c>
/// por-módulo nos controllers (via convention) isola cada documento.
/// </remarks>
[Collection(MonolitoHostCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class OpenApiPorModuloTests
{
    private static readonly string[] Modulos = ["configuracao", "organizacao", "selecao"];

    private readonly MonolitoPostgresFixture _fixture;

    public OpenApiPorModuloTests(MonolitoPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory(DisplayName = "Doc /openapi/{modulo}.json não vaza paths de outros módulos")]
    [InlineData("configuracao")]
    [InlineData("organizacao")]
    [InlineData("selecao")]
    public async Task DocDeModulo_NaoVazaPathsDeOutrosModulos(string modulo)
    {
        IReadOnlyList<string> paths = await ObterPathsAsync(modulo);

        // Todo path sob api/<outro>/ é vazamento.
        IEnumerable<string> prefixosDeOutros = Modulos
            .Where(m => !string.Equals(m, modulo, StringComparison.Ordinal))
            .Select(m => $"/api/{m}/");

        foreach (string prefixoOutro in prefixosDeOutros)
        {
            paths.Should().NotContain(
                p => p.StartsWith(prefixoOutro, StringComparison.Ordinal),
                $"o doc '{modulo}' não pode listar endpoints sob {prefixoOutro}");
        }
    }

    [Fact(DisplayName = "Doc /openapi/configuracao.json contém os próprios endpoints (api/configuracao/*)")]
    public async Task DocDeModulo_ContemSeusProprios()
    {
        IReadOnlyList<string> paths = await ObterPathsAsync("configuracao");

        paths.Should().Contain(
            p => p.StartsWith("/api/configuracao/", StringComparison.Ordinal),
            "o doc do módulo deve conter seus próprios endpoints");
    }

    private async Task<IReadOnlyList<string>> ObterPathsAsync(string modulo)
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri($"/openapi/{modulo}.json", UriKind.Relative));
        response.EnsureSuccessStatusCode();

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        if (!doc.RootElement.TryGetProperty("paths", out JsonElement paths))
        {
            return [];
        }

        return [.. paths.EnumerateObject().Select(p => p.Name)];
    }
}
