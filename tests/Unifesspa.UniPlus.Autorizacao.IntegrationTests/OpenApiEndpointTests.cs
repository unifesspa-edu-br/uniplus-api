namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests;

using System.IO;
using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;

/// <summary>
/// Smoke + drift check do endpoint <c>/openapi/autorizacao.json</c>:
/// runtime gera spec OpenAPI 3.x do módulo Autorização e compara
/// com o baseline committed em <c>contracts/openapi.autorizacao.json</c>.
/// </summary>
public sealed class OpenApiEndpointTests : IClassFixture<AutorizacaoApiFactory>
{
    private const string BaselineRelativePath = "contracts/openapi.autorizacao.json";
    private const string UpdateBaselineEnvVar = "UPDATE_OPENAPI_BASELINE";

    private readonly AutorizacaoApiFactory _factory;

    public OpenApiEndpointTests(AutorizacaoApiFactory factory)
    {
        _factory = factory;
    }

    [Fact(DisplayName = "GET /openapi/autorizacao.json retorna 200 com documento OpenAPI válido e metadata Uni+")]
    public async Task GetOpenApiDocument_RetornaSpecJsonComMetadataInjetada()
    {
        using HttpResponseMessage response = await ObterDocumentoAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        root.GetProperty("openapi").GetString().Should().StartWith("3.");
        root.GetProperty("info").GetProperty("title").GetString().Should().Be("Uni+ — Módulo Autorização");
        root.GetProperty("info").GetProperty("version").GetString().Should().Be("1.0.0");
    }

    [Fact(DisplayName = "O componente da situação do acesso explica que a situação é do perfil, e não de cada concessão")]
    public async Task SituacaoDoAcesso_ExplicaQueEDoPerfilENaoDaConcessao()
    {
        using HttpResponseMessage response = await ObterDocumentoAsync();
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        JsonElement componente = doc.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("SituacaoDoAcessoDoPerfil");

        componente.GetProperty("description").GetString().Should().Contain("não de cada concessão");
    }

    [Fact(DisplayName = "Spec runtime de Autorização bate com baseline committed em contracts/")]
    public async Task SpecRuntime_DeveCasarComBaselineCommitted()
    {
        CenarioDaBaseline cenario = new(this);
        await cenario.ExecutarAsync();
        if (cenario.Atualizada)
            return;

        File.Exists(cenario.Caminho).Should().BeTrue(
            $"baseline {BaselineRelativePath} precisa estar committed; rode `{UpdateBaselineEnvVar}=1 dotnet test --filter SpecRuntime` e commit o arquivo gerado.");
        string committedSpec = NormalizeJson(await File.ReadAllTextAsync(cenario.Caminho));
        cenario.Spec.Should().Be(committedSpec,
            $"contrato OpenAPI mudou — regerar a baseline com `{UpdateBaselineEnvVar}=1 dotnet test --filter SpecRuntime` e revisar o diff antes de commit (ADR-0030).");
    }

    private sealed class CenarioDaBaseline(OpenApiEndpointTests testes)
    {
        public string Caminho { get; } = ResolveRepoPath(BaselineRelativePath);
        public string Spec { get; private set; } = string.Empty;
        public bool Atualizada { get; private set; }

        public async Task ExecutarAsync()
        {
            using HttpResponseMessage response = await testes.ObterDocumentoAsync();
            response.EnsureSuccessStatusCode();
            Spec = NormalizeJson(await response.Content.ReadAsStringAsync());
            Atualizada = string.Equals(Environment.GetEnvironmentVariable(UpdateBaselineEnvVar), "1", StringComparison.Ordinal);
            if (Atualizada)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Caminho)!);
                await File.WriteAllTextAsync(Caminho, Spec + "\n");
            }
        }
    }

    private async Task<HttpResponseMessage> ObterDocumentoAsync()
    {
        using HttpClient client = _factory.CreateClient();
        return await client.GetAsync(new Uri("/openapi/autorizacao.json", UriKind.Relative));
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
