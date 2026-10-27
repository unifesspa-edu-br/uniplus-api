namespace Unifesspa.UniPlus.Selecao.IntegrationTests;

using System.IO;
using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Outbox.Cascading;

/// <summary>
/// Smoke + drift check do endpoint <c>/openapi/selecao.json</c>:
/// <list type="bullet">
///   <item><description>Smoke — runtime gera spec OpenAPI 3.x com metadata Uni+ aplicada
///   (ADR-0030, story #289).</description></item>
///   <item><description>Drift — compara o spec emitido com o baseline committed em
///   <c>contracts/openapi.selecao.json</c> (story #290). Falha o PR quando o
///   contrato muda sem regerar a baseline. Para regerar, rodar com
///   <c>UPDATE_OPENAPI_BASELINE=1</c> definido — o arquivo é reescrito e o
///   teste passa.</description></item>
/// </list>
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class OpenApiEndpointTests
{
    private const string BaselineRelativePath = "contracts/openapi.selecao.json";
    private const string UpdateBaselineEnvVar = "UPDATE_OPENAPI_BASELINE";

    private readonly CascadingFixture _fixture;

    public OpenApiEndpointTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "GET /openapi/selecao.json retorna 200 com documento OpenAPI válido e metadata Uni+")]
    public async Task GetOpenApiDocument_RetornaSpecJsonComMetadataInjetada()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri("/openapi/selecao.json", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        // OpenAPI 3.x — top-level: openapi, info, paths
        root.GetProperty("openapi").GetString().Should().StartWith("3.");
        root.GetProperty("info").GetProperty("title").GetString().Should().Be("Uni+ — Módulo Seleção");
        root.GetProperty("info").GetProperty("version").GetString().Should().Be("1.0.0");
        root.GetProperty("info").GetProperty("contact").GetProperty("email").GetString()
            .Should().Be("ctic@unifesspa.edu.br");

        // Servers injetados pelo UniPlusInfoTransformer (Produção, Homologação)
        JsonElement servers = root.GetProperty("servers");
        servers.GetArrayLength().Should().Be(2);
        servers[0].GetProperty("description").GetString().Should().Be("Produção");
        servers[1].GetProperty("description").GetString().Should().Be("Homologação");
    }

    [Fact(DisplayName = "Spec runtime de Selecao bate com baseline committed em contracts/")]
    public async Task SpecRuntime_DeveCasarComBaselineCommitted()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync(new Uri("/openapi/selecao.json", UriKind.Relative));
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
        // Reescreve com indentação canônica para que diffs apareçam por
        // mudança real, não por whitespace ou ordem de keys preservada
        // pelo serializador padrão.
        using JsonDocument document = JsonDocument.Parse(raw);
        return JsonSerializer.Serialize(document.RootElement, JsonNormalizationOptions);
    }

    private static readonly JsonSerializerOptions JsonNormalizationOptions = new()
    {
        WriteIndented = true,
    };

    private static string ResolveRepoPath(string relative)
    {
        string? current = AppContext.BaseDirectory;
        while (current is not null && !File.Exists(Path.Combine(current, "UniPlus.slnx")))
            current = Path.GetDirectoryName(current);

        if (current is null)
            throw new DirectoryNotFoundException("UniPlus.slnx não encontrado a partir de AppContext.BaseDirectory.");

        return Path.Combine(current, relative);
    }
}
