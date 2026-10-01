namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// Contrato HTTP da manutenção dos modelos de formulário pelo papel <c>plataforma-admin</c>
/// (UNI-REQ-0144): cadastro conferido contra o catálogo, leitura no formato da escrita, edição,
/// desativação e reativação.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class ModeloFormularioAdminEndpointTests
{
    private const string Base = "/api/configuracao/admin/modelos-formulario";

    private readonly ConfiguracaoEndpointFixture _fixture;

    public ModeloFormularioAdminEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Cadastrar modelo sem o papel plataforma-admin é proibido")]
    public async Task Criar_SemPapel_Retorna403()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        (await EnviarAsync(client, HttpMethod.Post, Base, Modelo(CodigoUnico()), papel: "candidato"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "Cadastro, leitura no formato da escrita, regravação do lido, desativação e reativação; a lista filtra")]
    public async Task CicloDoModelo()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarAsync(client, HttpMethod.Post, Base, Modelo(CodigoUnico()));
        criar.StatusCode.Should().Be(HttpStatusCode.Created, await criar.Content.ReadAsStringAsync());
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        JsonObject lido = await ObterAsync(client, id);
        lido["finalidade"]!.GetValue<string>().Should().Be("HABILITACAO");
        JsonNode condicao = lido["conteudo"]!["itens"]![1]!["precondicao"]![0]![0]!;
        condicao["fato"]!.GetValue<string>().Should().Be("QUILOMBOLA");
        condicao["valor"]!.GetValue<bool>().Should().BeTrue();

        // O conteúdo lido volta à escrita sem conversão.
        (await EnviarAsync(client, HttpMethod.Put, $"{Base}/{id}", new { nome = "Habilitação editada", descricao = (string?)null, tipoProcessoCodigo = (string?)null, conteudo = lido["conteudo"] }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await EnviarAsync(client, HttpMethod.Delete, $"{Base}/{id}", corpo: null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using (JsonDocument inativos = await ListarAsync(client, "?ativo=false&finalidade=HABILITACAO&tipoProcesso=QUALQUER_TIPO"))
        {
            inativos.RootElement.EnumerateArray().Should().Contain(m => m.GetProperty("id").GetGuid() == id
                && m.GetProperty("nome").GetString() == "Habilitação editada");
        }

        using (JsonDocument outraFinalidade = await ListarAsync(client, "?finalidade=INSCRICAO"))
        {
            outraFinalidade.RootElement.EnumerateArray().Should().NotContain(m => m.GetProperty("id").GetGuid() == id);
        }

        (await EnviarAsync(client, HttpMethod.Post, $"{Base}/{id}/ativacao", corpo: null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact(DisplayName = "Item de fato derivado é recusado pela conferência contra o catálogo")]
    public async Task Criar_ItemDeFatoDerivado_Retorna422()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Post, Base, Modelo(CodigoUnico(), fatoDoPrimeiroItem: "MODALIDADE"));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("errors").EnumerateArray()
            .Select(static e => (e.GetProperty("field").GetString(), e.GetProperty("code").GetString()))
            .Should().Contain(("conteudo.itens[0].fatoCodigo", "uniplus.item_formulario.fato_nao_coletavel"));
    }

    [Fact(DisplayName = "Edição recusada pelo catálogo não grava nada do que trazia")]
    public async Task Atualizar_RecusadaPeloCatalogo_NaoGrava()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarAsync(client, HttpMethod.Post, Base, Modelo(CodigoUnico()));
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();
        JsonObject lido = await ObterAsync(client, id);
        JsonNode conteudo = lido["conteudo"]!.DeepClone();
        conteudo["termos"] = new JsonArray(new JsonObject
        {
            ["codigo"] = "LGPD",
            ["ordem"] = 0,
            ["termoId"] = Guid.NewGuid(),
            ["versaoId"] = Guid.NewGuid(),
            ["exibicao"] = null,
            ["obrigatoriedade"] = "SEMPRE",
            ["predicadoObrigatoriedade"] = null,
        });

        (await EnviarAsync(client, HttpMethod.Put, $"{Base}/{id}", new { nome = "Nome que não deve ficar", descricao = (string?)null, tipoProcessoCodigo = (string?)null, conteudo }))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        JsonObject depois = await ObterAsync(client, id);
        depois["nome"]!.GetValue<string>().Should().Be("Habilitação Medicina 2027");
        depois["conteudo"]!["termos"]!.AsArray().Should().BeEmpty();
    }

    [Fact(DisplayName = "Tipo de processo com caractere nulo é recusado pelo modelo, sem chegar ao banco; na lista, a página é vazia")]
    public async Task TipoProcessoComCaractereNulo_RecusaSemErroDoBanco()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Dictionary<string, object?> modelo = new(JsonSerializer.SerializeToNode(Modelo(CodigoUnico()))!.AsObject()
            .Select(static p => KeyValuePair.Create(p.Key, (object?)p.Value?.DeepClone())))
        {
            ["tipoProcessoCodigo"] = "PSR\u0000",
        };

        HttpResponseMessage criar = await EnviarAsync(client, HttpMethod.Post, Base, modelo);
        HttpResponseMessage listar = await EnviarAsync(client, HttpMethod.Get, $"{Base}?tipoProcesso=%00", corpo: null);

        criar.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using (JsonDocument problema = JsonDocument.Parse(await criar.Content.ReadAsStringAsync()))
        {
            problema.RootElement.GetProperty("errors").EnumerateArray().Select(static e => e.GetProperty("code").GetString())
                .Should().Contain("uniplus.configuracao.modelo_formulario.texto_nao_gravavel");
        }

        listar.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument lista = JsonDocument.Parse(await listar.Content.ReadAsStringAsync());
        lista.RootElement.GetArrayLength().Should().Be(0);
    }

    [Fact(DisplayName = "A pré-visualização avalia o modelo com as respostas simuladas; modelo inexistente é 404, e sem o papel, 403")]
    public async Task PreVisualizar_RespostasSimuladas_AvaliaOModelo()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarAsync(client, HttpMethod.Post, Base, Modelo(CodigoUnico()));
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();
        object simulacao = new { respostas = new Dictionary<string, object> { ["QUILOMBOLA"] = false } };

        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Post, $"{Base}/{id}/pre-visualizacao", simulacao);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.uniplus.pre-visualizacao-modelo-formulario.v1+json");
        JsonObject resultado = JsonNode.Parse(await resposta.Content.ReadAsStringAsync())!.AsObject();
        JsonNode baixaRenda = resultado["itens"]!.AsArray().Single(static i => i!["fatoCodigo"]!.GetValue<string>() == "BAIXA_RENDA")!;
        baixaRenda["visivel"]!.GetValue<string>().Should().Be("FALSO");
        (await EnviarAsync(client, HttpMethod.Post, $"{Base}/{Guid.NewGuid()}/pre-visualizacao", simulacao)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await EnviarAsync(client, HttpMethod.Post, $"{Base}/{id}/pre-visualizacao", simulacao, papel: "candidato")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "Código já usado por outro modelo é recusado com conflito")]
    public async Task Criar_CodigoExistente_Retorna409()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        string codigo = CodigoUnico();
        (await EnviarAsync(client, HttpMethod.Post, Base, Modelo(codigo))).StatusCode.Should().Be(HttpStatusCode.Created);

        (await EnviarAsync(client, HttpMethod.Post, Base, Modelo(codigo))).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>
    /// Um modelo de habilitação com duas perguntas, a segunda exibida a quem respondeu sim à
    /// primeira, e o bloco de revisão e aceite que a finalidade exige.
    /// </summary>
    private static object Modelo(string codigo, string fatoDoPrimeiroItem = "QUILOMBOLA") => new
    {
        codigo,
        nome = "Habilitação Medicina 2027",
        finalidade = "HABILITACAO",
        conteudo = new
        {
            titulo = "Habilitação",
            etapas = new object[]
            {
                new { codigo = "DADOS", ordem = 0, tipo = "SECAO", bloco = (string?)null, titulo = "Dados", descricao = (string?)null, aviso = (string?)null },
                new { codigo = "REVISAO", ordem = 1, tipo = "BLOCO", bloco = "REVISAO_E_ACEITE", titulo = "Revisão", descricao = (string?)null, aviso = (string?)null },
            },
            itens = new object[]
            {
                new { fatoCodigo = fatoDoPrimeiroItem, ordem = 0, rotulo = "Quilombola", tipoRenderizacao = "BOOLEANO", obrigatoriedade = "SEMPRE", precondicao = (object?)null, etapaCodigo = "DADOS" },
                new
                {
                    fatoCodigo = "BAIXA_RENDA",
                    ordem = 1,
                    rotulo = "Baixa renda",
                    tipoRenderizacao = "BOOLEANO",
                    obrigatoriedade = "SEMPRE",
                    precondicao = new[] { new[] { new { fato = "QUILOMBOLA", operador = "IGUAL", valor = true } } },
                    etapaCodigo = "DADOS",
                },
            },
        },
    };

    private static string CodigoUnico() => $"MODELO_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";

    private static async Task<JsonObject> ObterAsync(HttpClient client, Guid id)
    {
        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Get, $"{Base}/{id}", corpo: null);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonNode.Parse(await resposta.Content.ReadAsStringAsync())!.AsObject();
    }

    private static async Task<JsonDocument> ListarAsync(HttpClient client, string filtros)
    {
        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Get, $"{Base}{filtros}", corpo: null);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
    }

    private static async Task<HttpResponseMessage> EnviarAsync(
        HttpClient client, HttpMethod metodo, string url, object? corpo, string papel = "plataforma-admin")
    {
        using HttpRequestMessage request = new(metodo, new Uri(url, UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, papel);
        if (metodo != HttpMethod.Get)
        {
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        }

        if (corpo is not null)
        {
            request.Content = JsonContent.Create(corpo);
        }

        return await client.SendAsync(request);
    }
}
