namespace Unifesspa.UniPlus.Selecao.IntegrationTests;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Outbox.Cascading;

using Unifesspa.UniPlus.IntegrationTests.Fixtures;

/// <summary>
/// Verifica o endpoint <c>/openapi/selecao.json</c>:
/// <list type="bullet">
///   <item><description>A aplicação gera o documento OpenAPI 3.x com os metadados Uni+
///   (ADR-0030).</description></item>
///   <item><description>A comparação com <c>contracts/openapi.selecao.json</c> identifica
///   mudanças no contrato sem atualização da referência aprovada.</description></item>
/// </list>
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class OpenApiEndpointTests
{
    private const string BaselineRelativePath = "contracts/openapi.selecao.json";

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
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        ConferirInformacoesDoDocumento(doc.RootElement);
        ConferirServidoresDoDocumento(doc.RootElement);
    }

    [Fact(DisplayName = "Spec runtime de Selecao bate com baseline committed em contracts/")]
    public async Task SpecRuntime_DeveCasarComBaselineCommitted()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        await ContratoOpenApiGravado.ConferirAsync(client, "/openapi/selecao.json", BaselineRelativePath);
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
    [InlineData("/api/selecao/admin/processos-seletivos/{id}/formularios/aplicacoes-de-modelo", "post")]
    [InlineData("/api/selecao/processos-seletivos/{processoSeletivoId}/produtos-previstos/{produtoId}/publicacoes", "post")]
    [InlineData("/api/selecao/processos-seletivos/{id}/fases/{faseCanonicaOrigemId}/motivos-de-decisao/{motivoDeDecisaoId}", "put")]
    [InlineData("/api/selecao/processos-seletivos/{id}/fases/{faseCanonicaOrigemId}/motivos-de-decisao/{motivoDeDecisaoId}", "delete")]
    [InlineData("/api/selecao/processos-seletivos/{id}/etapas/{etapaId}/motivos-de-decisao/{motivoDeDecisaoId}", "put")]
    [InlineData("/api/selecao/processos-seletivos/{id}/etapas/{etapaId}/motivos-de-decisao/{motivoDeDecisaoId}", "delete")]
    [InlineData("/api/selecao/admin/processos-seletivos/{id}/formularios/{codigo}", "put")]
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

    [Fact(DisplayName = "O PUT do rascunho publica que substitui o documento inteiro, que o bloco omitido é apagado, que a lista vazia continua presente e que a chave só vale para as partes que o esquema identifica por ela")]
    public async Task SubstituicaoDoRascunho_PublicaOEfeitoDaOmissao()
    {
        using JsonDocument spec = await LerSpecAsync();

        JsonElement operacao = spec.RootElement.GetProperty("paths").GetProperty("/api/selecao/rascunhos-de-processos-seletivos/{id}").GetProperty("put");

        operacao.GetProperty("summary").GetString().Should().NotBeNullOrWhiteSpace();
        operacao.GetProperty("description").GetString().Should()
            .Contain("Substitui o documento inteiro")
            .And.Contain("todo bloco que o corpo não traz é apagado")
            .And.Contain("Uma lista enviada como [] continua presente e declara nenhum item")
            .And.Contain("Devolva a chave de cada parte que o esquema identifica por chave")
            .And.Contain("A distribuição de vagas é identificada por ofertaCursoCodigo e não tem chave")
            .And.NotContain("a parte sem chave recusa o salvamento")
            .And.Contain("o envio pode ser repetido");
    }

    private static void ConferirInformacoesDoDocumento(JsonElement root)
    {
        // OpenAPI 3.x — top-level: openapi, info, paths
        root.GetProperty("openapi").GetString().Should().StartWith("3.");
        JsonElement info = root.GetProperty("info");
        info.GetProperty("title").GetString().Should().Be("Uni+ — Módulo Seleção");
        info.GetProperty("version").GetString().Should().Be("1.0.0");
        info.GetProperty("contact").GetProperty("email").GetString().Should().Be("ctic@unifesspa.edu.br");
    }

    // Servers injetados pelo UniPlusInfoTransformer (Produção, Homologação)
    private static void ConferirServidoresDoDocumento(JsonElement root)
    {
        JsonElement servers = root.GetProperty("servers");
        servers.GetArrayLength().Should().Be(2);
        servers[0].GetProperty("description").GetString().Should().Be("Produção");
        servers[1].GetProperty("description").GetString().Should().Be("Homologação");
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

    [Fact(DisplayName = "No contrato da Seleção, todo campo de texto que leva o código de um enum referencia o componente dos códigos")]
    public async Task CampoDeTextoComCodigoDeEnum_ReferenciaOComponenteDosCodigos()
    {
        using JsonDocument spec = await LerSpecAsync();

        CodigosDeEnumNoContrato.CamposComTextoCru(spec.RootElement).Should().BeEmpty(
            "o campo que leva código de enum de contrato publica o componente dos códigos do enum (ADR-0176)");
        CodigosDeEnumNoContrato.ReferenciaOsCodigos(spec.RootElement, "ApresentacaoDoCampoDto", "tipoDeCampo").Should().BeTrue();
        CodigosDeEnumNoContrato.ReferenciaOsCodigos(spec.RootElement, "CondicaoPrecondicaoDto", "operador").Should().BeTrue();
    }

    [Fact(DisplayName = "A situação do processo referencia os componentes e códigos próprios")]
    public async Task SituacaoDoProcesso_NoContrato_ReferenciaOsCodigos()
    {
        using JsonDocument spec = await LerSpecAsync();
        JsonElement schemas = spec.RootElement.GetProperty("components").GetProperty("schemas");

        ConferirSituacaoNosDtos(schemas);

        schemas.GetProperty("SituacaoDoProcessoSeletivo").GetProperty("properties").GetProperty("codigo").GetProperty("$ref").GetString()
            .Should().Be("#/components/schemas/SituacaoDoProcessoSeletivoCodigo");
        schemas.GetProperty("SituacaoDoProcessoSeletivoCodigo").GetProperty("enum").EnumerateArray().Select(static valor => valor.GetString())
            .Should().Equal("RASCUNHO", "PUBLICADO", "ENCERRADO", "CANCELADO");
    }

    private static void ConferirSituacaoNosDtos(JsonElement schemas)
    {
        foreach (string nome in new[] { "ProcessoSeletivoDto", "ProcessoSeletivoResumoDto" })
        {
            JsonElement propriedades = schemas.GetProperty(nome).GetProperty("properties");
            propriedades.TryGetProperty("status", out _).Should().BeFalse();
            propriedades.GetProperty("situacao").GetProperty("$ref").GetString()
                .Should().Be("#/components/schemas/SituacaoDoProcessoSeletivo");
        }
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

    [Fact(DisplayName = "O valor dos itens tem a mesma descrição e não enumera opções")]
    public async Task ValorDosItens_NoContrato_TemDescricaoUnica()
    {
        using JsonDocument spec = await LerSpecAsync();
        JsonElement schemas = spec.RootElement.GetProperty("components").GetProperty("schemas");
        JsonElement administrativo = ValorDoItem(schemas, "ItemDeRespostaDto");
        JsonElement comprovante = ValorDoItem(schemas, "ItemDoComprovanteDto");
        administrativo.GetProperty("description").GetString().Should().Be(comprovante.GetProperty("description").GetString())
            .And.Contain("O rótulo pode vir das opções declaradas pelo processo e congeladas na publicação ou dos valores de domínio congelados; para UF, vem da lista oficial");
        schemas.GetProperty("ExibicaoDoValorDoItemCodigo").GetProperty("description").GetString()
            !.Split('\n').Single(static linha => linha.Contains("`SITUACAO`:", StringComparison.Ordinal))
            .Should().Contain("A tela mostra o rótulo numa etiqueta, sem mudar a forma do valor");
    }

    private static JsonElement ValorDoItem(JsonElement schemas, string nome)
    {
        JsonElement valor = schemas.GetProperty(nome).GetProperty("properties").GetProperty("valor");
        valor.TryGetProperty("enum", out _).Should().BeFalse();
        JsonElement alternativa = valor.GetProperty("oneOf").EnumerateArray()
            .Single(static alternativa => alternativa.TryGetProperty("$ref", out _));
        alternativa.TryGetProperty("enum", out _).Should().BeFalse();
        alternativa.GetProperty("$ref").GetString().Should().Be("#/components/schemas/JsonElement");
        return alternativa;
    }

    [Fact(DisplayName = "A fase do cronograma explica que a gravação a substitui inteira e que a regra de homologação ausente é apagada")]
    public async Task FaseDoCronograma_DescreveASubstituicaoDaRegraDeHomologacao()
    {
        using JsonDocument spec = await LerSpecAsync();

        JsonElement regra = spec.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("FaseCronogramaInput")
            .GetProperty("properties").GetProperty("regraDeHomologacao");

        regra.GetProperty("oneOf").EnumerateArray().Select(static alternativa => alternativa.TryGetProperty("description", out JsonElement texto) ? texto.GetString() : null)
            .Should().ContainSingle(static texto => texto != null)
            .Which.Should()
            .Contain("A gravação substitui a fase inteira")
            .And.Contain("ausente ou nulo")
            .And.Contain("a regra que ela tinha é apagada")
            .And.Contain("Quem grava devolve a regra lida");
    }

    private async Task<JsonDocument> LerSpecAsync()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync(new Uri("/openapi/selecao.json", UriKind.Relative));
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }
}
