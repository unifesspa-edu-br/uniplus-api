namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests;

using System.IO;
using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Infrastructure;

/// <summary>
/// Smoke + drift check do endpoint <c>/openapi/organizacao.json</c>:
/// runtime gera spec OpenAPI 3.x do módulo Organização Institucional e compara
/// com o baseline committed em <c>contracts/openapi.organizacao.json</c>.
/// </summary>
public sealed class OpenApiEndpointTests : IClassFixture<OrganizacaoApiFactory>
{
    private const string BaselineRelativePath = "contracts/openapi.organizacao.json";
    private const string UpdateBaselineEnvVar = "UPDATE_OPENAPI_BASELINE";

    private readonly OrganizacaoApiFactory _factory;

    public OpenApiEndpointTests(OrganizacaoApiFactory factory)
    {
        _factory = factory;
    }

    [Fact(DisplayName = "GET /openapi/organizacao.json retorna 200 com documento OpenAPI válido e metadata Uni+")]
    public async Task GetOpenApiDocument_RetornaSpecJsonComMetadataInjetada()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri("/openapi/organizacao.json", UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        root.GetProperty("openapi").GetString().Should().StartWith("3.");
        root.GetProperty("info").GetProperty("title").GetString().Should().Be("Uni+ — Módulo Organização Institucional");
        root.GetProperty("info").GetProperty("version").GetString().Should().Be("1.0.0");
    }

    [Fact(DisplayName = "Spec runtime de Organização bate com baseline committed em contracts/")]
    public async Task SpecRuntime_DeveCasarComBaselineCommitted()
    {
        using HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync(new Uri("/openapi/organizacao.json", UriKind.Relative));
        response.EnsureSuccessStatusCode();

        string runtimeSpec = NormalizeJson(await response.Content.ReadAsStringAsync());
        string baselinePath = ResolveRepoPath(BaselineRelativePath);

        if (string.Equals(Environment.GetEnvironmentVariable(UpdateBaselineEnvVar), "1", StringComparison.Ordinal))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(baselinePath)!);
            await File.WriteAllTextAsync(baselinePath, runtimeSpec + "\n");
            return;
        }

        File.Exists(baselinePath).Should().BeTrue(
            $"baseline {BaselineRelativePath} precisa estar committed; rode `{UpdateBaselineEnvVar}=1 dotnet test --filter SpecRuntime` e commit o arquivo gerado.");

        string committedSpec = NormalizeJson(await File.ReadAllTextAsync(baselinePath));

        runtimeSpec.Should().Be(committedSpec,
            $"contrato OpenAPI mudou — regerar a baseline com `{UpdateBaselineEnvVar}=1 dotnet test --filter SpecRuntime` e revisar o diff antes de commit (ADR-0030).");
    }

    private static string NormalizeJson(string raw)
    {
        using JsonDocument document = JsonDocument.Parse(raw);
        return JsonSerializer.Serialize(document.RootElement, JsonNormalizationOptions);
    }

    private static readonly JsonSerializerOptions JsonNormalizationOptions = new()
    {
        WriteIndented = true,
    };

    private static string ResolveRepoPath(string relative)
    {
        // Caminho deve ser relativo à raiz do repositório — um caminho enraizado
        // faria Path.Combine descartar `current` silenciosamente.
        ArgumentException.ThrowIfNullOrWhiteSpace(relative);
        if (Path.IsPathRooted(relative))
            throw new ArgumentException("Caminho deve ser relativo à raiz do repositório.", nameof(relative));

        string? current = AppContext.BaseDirectory;
        while (current is not null && !File.Exists(Path.Combine(current, "UniPlus.slnx")))
            current = Path.GetDirectoryName(current);

        if (current is null)
            throw new DirectoryNotFoundException("UniPlus.slnx não encontrado a partir de AppContext.BaseDirectory.");

        return Path.Combine(current, relative);
    }
}
