namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.FatosCandidato;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// Contrato HTTP da manutenção do catálogo de fatos pelo papel <c>plataforma-admin</c>
/// (UNI-REQ-0143, ADR-0136): cadastro de fato declarado, edição de nome e descrição, desativação
/// e valores de domínio.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class FatoCandidatoAdminEndpointTests
{
    private const string Base = "/api/configuracao/admin/fatos-candidato";
    private static readonly Guid IdDeCorRaca = Guid.Parse("fa700000-0000-7000-8000-000000000001");

    private readonly ConfiguracaoEndpointFixture _fixture;

    public FatoCandidatoAdminEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Cadastrar fato sem o papel plataforma-admin é proibido")]
    public async Task Criar_SemPapel_Retorna403()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(Base, UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "candidato");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(FatoBooleano(CodigoUnico()));

        (await client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "Cadastro, edição, desativação e reativação do fato declarado; a lista filtra por ativo")]
    public async Task CicloDoFatoDeclarado()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        string codigo = CodigoUnico();

        HttpResponseMessage criar = await EnviarAsync(client, HttpMethod.Post, Base, FatoBooleano(codigo));
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        using (JsonDocument fato = await ObterAsync(client, id))
        {
            fato.RootElement.GetProperty("origem").GetString().Should().Be("DECLARADO");
            fato.RootElement.GetProperty("binding").GetString().Should().Be($"CAMPO_INSCRICAO:{codigo}");
            fato.RootElement.GetProperty("sistema").GetBoolean().Should().BeFalse();
        }

        (await EnviarAsync(client, HttpMethod.Put, $"{Base}/{id}", new { nome = "Nome editado", descricao = (string?)null }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await EnviarAsync(client, HttpMethod.Delete, $"{Base}/{id}", corpo: null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage inativos = await EnviarAsync(client, HttpMethod.Get, $"{Base}?ativo=false&origem=DECLARADO", corpo: null);
        inativos.StatusCode.Should().Be(HttpStatusCode.OK);
        using (JsonDocument lista = JsonDocument.Parse(await inativos.Content.ReadAsStringAsync()))
        {
            lista.RootElement.EnumerateArray().Should().Contain(item => item.GetProperty("id").GetGuid() == id
                && item.GetProperty("nome").GetString() == "Nome editado");
            lista.RootElement.EnumerateArray().Should().OnlyContain(item => !item.GetProperty("ativo").GetBoolean()
                && item.GetProperty("origem").GetString() == "DECLARADO");
            lista.RootElement.EnumerateArray().Should().NotContain(item => item.GetProperty("codigo").GetString() == "COR_RACA");
        }

        (await EnviarAsync(client, HttpMethod.Post, $"{Base}/{id}/ativacao", corpo: null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact(DisplayName = "Origem desconhecida no filtro da lista devolve página vazia, não erro")]
    public async Task Listar_OrigemDesconhecida_PaginaVazia()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Get, $"{Base}?origem=declarado", corpo: null);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument lista = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        lista.RootElement.GetArrayLength().Should().Be(0);
    }

    [Fact(DisplayName = "Código já usado, inclusive por fato de sistema, é recusado com conflito")]
    public async Task Criar_CodigoExistente_Retorna409()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        (await EnviarAsync(client, HttpMethod.Post, Base, FatoBooleano("COR_RACA")))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact(DisplayName = "Fato de sistema não é desativado pela manutenção")]
    public async Task Desativar_FatoDeSistema_Retorna422()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        (await EnviarAsync(client, HttpMethod.Delete, $"{Base}/{IdDeCorRaca}", corpo: null))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "Valor de domínio é acrescentado e desativado, nunca removido; desativar de novo é recusado")]
    public async Task ValoresDeDominio()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarAsync(client, HttpMethod.Post, Base, new
        {
            codigo = CodigoUnico(),
            nome = "Forma de conclusão",
            dominio = "CATEGORICO",
            cardinalidade = "ESCALAR",
            fonteValores = "GLOBAL",
            pontoResolucao = "INSCRICAO",
            escopo = "CANDIDATO",
            classificacaoProtecao = "PESSOAL",
            finalidadeTratamento = "Verificação dos requisitos do processo seletivo.",
            hipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL",
        });
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        (await EnviarAsync(client, HttpMethod.Post, $"{Base}/{id}/valores", new { codigo = "EJA", descricao = "Educação de jovens e adultos", ordem = 0 }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await EnviarAsync(client, HttpMethod.Delete, $"{Base}/{id}/valores/EJA", corpo: null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await EnviarAsync(client, HttpMethod.Delete, $"{Base}/{id}/valores/EJA", corpo: null))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        using JsonDocument fato = await ObterAsync(client, id);
        JsonElement valor = fato.RootElement.GetProperty("valores").EnumerateArray().Single();
        valor.GetProperty("codigo").GetString().Should().Be("EJA");
        valor.GetProperty("ativo").GetBoolean().Should().BeFalse();
    }

    private static object FatoBooleano(string codigo) => new
    {
        codigo,
        nome = "Vínculo com outra instituição pública",
        dominio = "BOOLEANO",
        cardinalidade = "ESCALAR",
        pontoResolucao = "HABILITACAO",
        escopo = "CANDIDATO",
        classificacaoProtecao = "PESSOAL",
        finalidadeTratamento = "Verificação dos requisitos de matrícula do processo seletivo.",
        hipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL",
    };

    private static string CodigoUnico() => $"FATO_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";

    private static async Task<JsonDocument> ObterAsync(HttpClient client, Guid id)
    {
        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Get, $"{Base}/{id}", corpo: null);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
    }

    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient client, HttpMethod metodo, string url, object? corpo)
    {
        using HttpRequestMessage request = new(metodo, new Uri(url, UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
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
