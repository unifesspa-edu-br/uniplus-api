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

    [Theory(DisplayName = "Os POST de publicação declaram o header Location no 201")]
    [InlineData("/api/selecao/processos-seletivos/{processoSeletivoId}/atos")]
    [InlineData("/api/selecao/processos-seletivos/{processoSeletivoId}/produtos-previstos/{produtoId}/publicacoes")]
    public async Task PublicacaoDeAto_DeclaraLocationNoCreated(string rota)
    {
        using JsonDocument spec = await LerSpecAsync();

        JsonElement headers = spec.RootElement.GetProperty("paths").GetProperty(rota)
            .GetProperty("post").GetProperty("responses").GetProperty("201").GetProperty("headers");

        headers.GetProperty("Location").GetProperty("description").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Theory(DisplayName = "As mutações sujeitas à trava do processo, com Idempotency-Key, declaram o 503 guardado pela chave, sem o Retry-After")]
    [InlineData("/api/selecao/processos-seletivos/{processoSeletivoId}/atos", "post")]
    [InlineData("/api/selecao/processos-seletivos/{id}/publicacao", "post")]
    [InlineData("/api/selecao/processos-seletivos/{id}/retificacoes", "post")]
    [InlineData("/api/selecao/processos-seletivos/{id}/retificacao-em-curso", "post")]
    [InlineData("/api/selecao/processos-seletivos/{id}/retificacao-em-curso", "put")]
    [InlineData("/api/selecao/processos-seletivos/{id}/retificacao-em-curso", "delete")]
    [InlineData("/api/selecao/processos-seletivos/{id}/retificacao-em-curso/fechamento", "post")]
    [InlineData("/api/selecao/admin/processos-seletivos/{id}/formularios/aplicacoes-de-modelo", "post")]
    [InlineData("/api/selecao/processos-seletivos/{processoSeletivoId}/produtos-previstos/{produtoId}/publicacoes", "post")]
    [InlineData("/api/selecao/processos-seletivos/{id}/fases/{faseCanonicaOrigemId}/motivos-de-decisao/{motivoDeDecisaoId}", "put")]
    [InlineData("/api/selecao/processos-seletivos/{id}/fases/{faseCanonicaOrigemId}/motivos-de-decisao/{motivoDeDecisaoId}", "delete")]
    [InlineData("/api/selecao/processos-seletivos/{id}/etapas/{etapaId}/motivos-de-decisao/{motivoDeDecisaoId}", "put")]
    [InlineData("/api/selecao/processos-seletivos/{id}/etapas/{etapaId}/motivos-de-decisao/{motivoDeDecisaoId}", "delete")]
    [InlineData("/api/selecao/admin/processos-seletivos/{id}/formularios/{finalidade}", "put")]
    [InlineData("/api/selecao/processos-seletivos/{id}/etapas", "put")]
    [InlineData("/api/selecao/processos-seletivos/{id}/classificacao", "put")]
    [InlineData("/api/selecao/processos-seletivos/{id}/cronograma-fases", "put")]
    public async Task MutacaoComChave_DeclaraEsperaEsgotadaGuardadaSemRetryAfter(string rota, string metodo)
    {
        using JsonDocument spec = await LerSpecAsync();

        JsonElement recusa = RecusaPorEsperaEsgotada(spec, rota, metodo);

        recusa.GetProperty("description").GetString().Should().Contain("nova chave");
        recusa.TryGetProperty("headers", out _).Should().BeFalse("a recusa guardada pela chave não traz prazo de nova tentativa");
    }

    [Theory(DisplayName = "O lote de notas e o envio ao INEP, sem Idempotency-Key, declaram o 503 sem Retry-After e sem falar em chave")]
    [InlineData("/api/selecao/processos-seletivos/{processoSeletivoId}/etapas/{etapaId}/edicoes-do-enem/{edicao}/lotes-de-notas", "post")]
    [InlineData("/api/selecao/processos-seletivos/{processoSeletivoId}/etapas/{etapaId}/edicoes-do-enem/{edicao}/envios-ao-inep", "post")]
    public async Task MutacaoSemChave_DeclaraEsperaEsgotadaSemFalarEmChave(string rota, string metodo)
    {
        using JsonDocument spec = await LerSpecAsync();

        JsonElement recusa = RecusaPorEsperaEsgotada(spec, rota, metodo);

        recusa.GetProperty("description").GetString().Should().Contain("envie de novo").And.NotContain("chave");
        recusa.TryGetProperty("headers", out _).Should().BeFalse();
    }

    [Fact(DisplayName = "Nenhuma operação do contrato declara o header Retry-After")]
    public async Task Contrato_NaoDeclaraRetryAfter()
    {
        using JsonDocument spec = await LerSpecAsync();

        spec.RootElement.GetRawText().Should().NotContain("Retry-After\":");
    }

    [Theory(DisplayName = "As consultas por POST, que não travam o processo, não declaram o 503 da espera esgotada")]
    [InlineData("/api/selecao/processos-seletivos/{id}/distribuicao-vagas/simulacao")]
    [InlineData("/api/selecao/processos-seletivos/{id}/pre-visualizacao")]
    public async Task ConsultaPorPost_NaoDeclaraEsperaEsgotada(string rota)
    {
        using JsonDocument spec = await LerSpecAsync();

        JsonElement respostas = spec.RootElement.GetProperty("paths").GetProperty(rota).GetProperty("post").GetProperty("responses");

        respostas.TryGetProperty("503", out _).Should().BeFalse();
    }

    private static JsonElement RecusaPorEsperaEsgotada(JsonDocument spec, string rota, string metodo)
    {
        JsonElement recusa = spec.RootElement.GetProperty("paths").GetProperty(rota)
            .GetProperty(metodo).GetProperty("responses").GetProperty("503");

        recusa.GetProperty("description").GetString().Should().Contain("uniplus.concorrencia.espera_esgotada");
        recusa.GetProperty("content").GetProperty("application/problem+json").GetProperty("schema").GetProperty("$ref")
            .GetString().Should().EndWith("/ProblemDetails");
        return recusa;
    }

    [Fact(DisplayName = "O documento do ato no certame explica que só entra na lista depois da cópia ao acervo")]
    public async Task DocumentoDoAtoDoCertame_ExplicaPorQueOEnderecoNuncaENulo()
    {
        using JsonDocument spec = await LerSpecAsync();

        JsonElement schemas = spec.RootElement.GetProperty("components").GetProperty("schemas");
        schemas.GetProperty("DocumentoDoAtoCertameDto").GetProperty("properties").GetProperty("urlDownload")
            .GetProperty("description").GetString().Should().Contain("Nunca é nulo");
        schemas.GetProperty("EditalPublicadoCertameDto").GetProperty("properties").GetProperty("documentos")
            .GetProperty("description").GetString().Should().Contain("depois da cópia ao acervo");
    }

    [Fact(DisplayName = "O endereço do documento do ato é obrigatório e não nulo; o do edital, do documento do edital e do modelo é obrigatório e pode ser nulo")]
    public async Task UrlDownloadDoCertame_NuloSoOndeAAusenciaExiste()
    {
        using JsonDocument spec = await LerSpecAsync();

        JsonElement schemas = spec.RootElement.GetProperty("components").GetProperty("schemas");

        ExigirUrlDownload(schemas, "DocumentoDoAtoCertameDto", nulo: false);
        ExigirUrlDownload(schemas, "EditalPublicadoCertameDto", nulo: true);
        ExigirUrlDownload(schemas, "DocumentoEditalCertameDto", nulo: true);
        ExigirUrlDownload(schemas, "ModeloDocumentalCertameDto", nulo: true);
    }

    [Fact(DisplayName = "A consulta de um ato declara o 200 do ato e o 404 nomeado")]
    public async Task ConsultaDeUmAto_DeclaraOkENaoEncontrado()
    {
        using JsonDocument spec = await LerSpecAsync();

        JsonElement responses = spec.RootElement.GetProperty("paths")
            .GetProperty("/api/selecao/processos-seletivos/{processoSeletivoId}/atos/{atoId}")
            .GetProperty("get").GetProperty("responses");

        responses.TryGetProperty("200", out _).Should().BeTrue();
        responses.GetProperty("404").GetProperty("description").GetString()
            .Should().Contain("uniplus.selecao.ato_do_processo.nao_encontrado");
    }

    private static void ExigirUrlDownload(JsonElement schemas, string schema, bool nulo)
    {
        JsonElement definicao = schemas.GetProperty(schema);
        definicao.GetProperty("required").EnumerateArray().Select(static e => e.GetString())
            .Should().Contain("urlDownload", $"{schema} sempre traz o campo");
        JsonElement tipo = definicao.GetProperty("properties").GetProperty("urlDownload").GetProperty("type");
        bool aceitaNulo = tipo.ValueKind == JsonValueKind.Array
            && tipo.EnumerateArray().Any(static t => t.GetString() == "null");
        aceitaNulo.Should().Be(nulo, $"{schema}.urlDownload {(nulo ? "pode" : "não pode")} ser nulo");
    }

    private async Task<JsonDocument> LerSpecAsync()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync(new Uri("/openapi/selecao.json", UriKind.Relative));
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
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
