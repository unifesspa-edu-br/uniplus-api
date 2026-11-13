namespace Unifesspa.UniPlus.Host.IntegrationTests;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Decisao;
using Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;

/// <summary>
/// O catálogo de permissões montado pelo composition root real e os dois endpoints
/// que o consultam (ADR-0151).
/// </summary>
[Collection(MonolitoHostCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class CatalogoDePermissoesNoHostTests : IAsyncLifetime
{
    private const string Catalogo = "/api/autorizacao/permissoes";
    private const string Efetivas = "/api/autorizacao/permissoes/efetivas";

    private const string CodigoDoCatalogo = "autorizacao:permissoes:consultar-catalogo";
    private const string Leandro = "leandro.viana";
    private const string Betina = "betina.neves";

    private readonly MonolitoPostgresFixture _fixture;

    public CatalogoDePermissoesNoHostTests(MonolitoPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    // As personas compartilham o banco da coleção: cada teste começa sem concessão alguma.
    public async Task InitializeAsync()
    {
        foreach (string sub in new[] { Leandro, Betina })
        {
            await ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, sub);
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "Todo enum de permissões do produto está registrado no catálogo da aplicação")]
    public void Enums_EstaoTodosRegistrados()
    {
        // Um enum esquecido ficaria fora do catálogo, do endpoint e de toda conferência.
        // Carregados da pasta de saída: o CLR só carrega o assembly quando um tipo dele é usado.
        Type[] enums = [.. Directory.GetFiles(AppContext.BaseDirectory, "Unifesspa.UniPlus.*.dll")
            .Select(static caminho => Assembly.LoadFrom(caminho))
            .SelectMany(static a => a.GetTypes())
            .Where(static t => t.IsEnum && t.GetCustomAttribute<ModuloDePermissoesAttribute>() is not null)];

        enums.Should().Contain(typeof(PermissaoSelecao)).And.Contain(typeof(PermissaoAutorizacao));

        IEnumerable<Type> registrados = _fixture.Factory.Services.GetServices<IFonteDeCatalogo>()
            .Select(static f => f.TipoDoEnum);

        registrados.Should().BeEquivalentTo(enums);
    }

    [Fact(DisplayName = "O catálogo publica a manutenção dos motivos e o papel do client no Keycloak que restou")]
    public void Codigos_ContemOsMotivosEOPapelDoKeycloak()
    {
        CatalogoDaAplicacao catalogo = _fixture.Factory.Services.GetRequiredService<CatalogoDaAplicacao>();

        // Só a consulta do catálogo continua papel do client no realm; a manutenção dos motivos é
        // concedida por perfil, e o código dela existe apenas no catálogo em código.
        catalogo.Definicoes.Select(static d => d.Codigo).Should().Contain(
        [
            "selecao:motivos-de-decisao:manter",
            "autorizacao:permissoes:consultar-catalogo",
        ]);
    }

    [Fact(DisplayName = "Toda verificação de contexto declarada no catálogo tem implementação")]
    public void Verificacoes_DeclaradasTemImplementacao()
    {
        using IServiceScope escopo = _fixture.Factory.Services.CreateScope();
        CatalogoDaAplicacao catalogo = escopo.ServiceProvider.GetRequiredService<CatalogoDaAplicacao>();

        string[] implementadas = [.. typeof(GrantEfetivoAplicavelCheck).Assembly.GetTypes()
            .Where(static t => t is { IsAbstract: false, IsInterface: false }
                && typeof(IPermissaoCheck).IsAssignableFrom(t)
                && t != typeof(ImpedimentoPorInscricaoCheck)
                && t != typeof(DesignacaoNaBancaCheck))
            .Select(static t => ((IPermissaoCheck)Activator.CreateInstance(t)!).Nome)
            .Append(DesignacaoNaBancaCheck.NomeCanonico)];

        catalogo.Definicoes.SelectMany(static d => d.Requisito.VerificacoesDeContexto)
            .Should().BeSubsetOf(implementadas)
            .And.NotContain([GrantEfetivoAplicavelCheck.NomeCanonico, ImpedimentoPorInscricaoCheck.NomeCanonico]);
    }

    [Fact(DisplayName = "GET do catálogo sem token responde 401")]
    public async Task Catalogo_SemToken_401()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await client.GetAsync(new Uri(Catalogo, UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "GET do catálogo sem a permissão responde 403")]
    public async Task Catalogo_SemPermissao_403()
    {
        using HttpClient client = await ClienteComConcessaoAsync(PermissaoSelecao.MotivosDeDecisaoManter.Codigo);

        using HttpResponseMessage resposta = await client.GetAsync(new Uri(Catalogo, UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "GET do catálogo com a permissão lista as permissões agrupadas por módulo e resource")]
    public async Task Catalogo_ComPermissao_ListaAgrupada()
    {
        using HttpClient client = await ClienteComConcessaoAsync(CodigoDoCatalogo);

        using HttpResponseMessage resposta = await client.GetAsync(new Uri(Catalogo, UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.uniplus.permissao-catalogo.v1+json");

        JsonElement corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        JsonElement selecao = corpo.EnumerateArray().Single(m => m.GetProperty("modulo").GetString() == "selecao");
        JsonElement resource = selecao.GetProperty("resources").EnumerateArray()
            .Single(r => r.GetProperty("resource").GetString() == "motivos-de-decisao");

        resource.GetProperty("permissoes").EnumerateArray()
            .Select(p => p.GetProperty("codigo").GetString())
            .Should().BeEquivalentTo("selecao:motivos-de-decisao:manter");
        resource.GetProperty("permissoes")[0].GetProperty("nome").GetString().Should().NotBeNullOrWhiteSpace();
        resource.GetProperty("permissoes")[0].GetProperty("sensibilidade").GetString().Should().Be("interno");
    }

    [Fact(DisplayName = "GET do catálogo com Accept de outro tipo responde 406")]
    public async Task Catalogo_AcceptIncompativel_406()
    {
        using HttpClient client = await ClienteComConcessaoAsync(CodigoDoCatalogo);
        client.DefaultRequestHeaders.Accept.ParseAdd("text/csv");

        using HttpResponseMessage resposta = await client.GetAsync(new Uri(Catalogo, UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.NotAcceptable);
    }

    [Fact(DisplayName = "Versão não suportada responde 406 com o código canônico e as versões servidas")]
    public async Task Catalogo_VersaoNaoSuportada_406CanonicoComVersoes()
    {
        using HttpClient client = await ClienteComConcessaoAsync(CodigoDoCatalogo);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.uniplus.permissao-catalogo.v99+json");

        using HttpResponseMessage resposta = await client.GetAsync(new Uri(Catalogo, UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.NotAcceptable);
        JsonElement corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        corpo.GetProperty("code").GetString().Should().Be("uniplus.contract.versao_nao_suportada");
        corpo.GetProperty("available_versions").EnumerateArray().Select(v => v.GetInt32()).Should().Equal(1);
    }

    [Fact(DisplayName = "Exclusão explícita da representação (q=0) prevalece sobre o curinga e responde 406")]
    public async Task Catalogo_ExclusaoExplicitaComCuringa_406()
    {
        using HttpClient client = await ClienteComConcessaoAsync(CodigoDoCatalogo);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.uniplus.permissao-catalogo.v1+json;q=0, */*;q=1");

        using HttpResponseMessage resposta = await client.GetAsync(new Uri(Catalogo, UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.NotAcceptable);
    }

    [Fact(DisplayName = "Faixa específica aceita prevalece sobre curinga excluído")]
    public async Task Catalogo_FaixaEspecificaComCuringaExcluido_200()
    {
        using HttpClient client = await ClienteComConcessaoAsync(CodigoDoCatalogo);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.uniplus.permissao-catalogo.v1+json;q=1, */*;q=0");

        using HttpResponseMessage resposta = await client.GetAsync(new Uri(Catalogo, UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "A autorização precede a negociação de mídia: sem a permissão e com Accept inválido, 403 em problem+json, e não 406")]
    public async Task Catalogo_SemPermissaoComAcceptInvalido_403ProblemJson()
    {
        using HttpClient client = Cliente(Betina);
        client.DefaultRequestHeaders.Accept.ParseAdd("text/csv");

        using HttpResponseMessage resposta = await client.GetAsync(new Uri(Catalogo, UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact(DisplayName = "O 406 do catálogo é problem+json, e o 200 informa Vary: Accept")]
    public async Task Catalogo_CorposEVary()
    {
        using HttpClient client = await ClienteComConcessaoAsync(CodigoDoCatalogo);
        using HttpResponseMessage ok = await client.GetAsync(new Uri(Catalogo, UriKind.Relative));
        ok.Headers.Vary.Should().Contain("Accept");

        using HttpRequestMessage invalido = new(HttpMethod.Get, new Uri(Catalogo, UriKind.Relative));
        invalido.Headers.Accept.ParseAdd("text/csv");
        using HttpResponseMessage recusada = await client.SendAsync(invalido);

        recusada.StatusCode.Should().Be(HttpStatusCode.NotAcceptable);
        recusada.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact(DisplayName = "As efetivas respondem 406 a Accept inválido e a q=0 da própria representação, e informam Vary: Accept no 200")]
    public async Task Efetivas_AcceptEVary()
    {
        using HttpClient client = Cliente(Betina);
        using HttpResponseMessage ok = await client.GetAsync(new Uri(Efetivas, UriKind.Relative));
        ok.StatusCode.Should().Be(HttpStatusCode.OK);
        ok.Headers.Vary.Should().Contain("Accept");

        foreach (string accept in new[] { "text/csv", "application/vnd.uniplus.permissao-efetiva.v1+json;q=0, */*;q=1" })
        {
            using HttpRequestMessage pedido = new(HttpMethod.Get, new Uri(Efetivas, UriKind.Relative));
            pedido.Headers.Accept.ParseAdd(accept);
            using HttpResponseMessage resposta = await client.SendAsync(pedido);

            resposta.StatusCode.Should().Be(HttpStatusCode.NotAcceptable, accept);
        }
    }

    [Fact(DisplayName = "GET das efetivas sem token responde 401")]
    public async Task Efetivas_SemToken_401()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await client.GetAsync(new Uri(Efetivas, UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "GET das efetivas devolve exatamente as permissões das concessões ativas, e o papel no token não entra")]
    public async Task Efetivas_DevolveSoAsDasConcessoes()
    {
        await ConcessoesDeTeste.ConcederPermissoesAsync(
            _fixture.Factory.Services, Leandro, PermissaoSelecao.MotivosDeDecisaoManter.Codigo);
        using HttpClient client = Cliente(Leandro, papeisDeRealm: CodigoDoCatalogo);

        using HttpResponseMessage resposta = await client.GetAsync(new Uri(Efetivas, UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.uniplus.permissao-efetiva.v1+json");

        JsonElement corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        corpo.GetArrayLength().Should().Be(1);
        corpo[0].GetProperty("codigo").GetString().Should().Be("selecao:motivos-de-decisao:manter");
        corpo[0].GetProperty("nome").GetString().Should().Be("Manter motivos de decisão");
    }

    [Fact(DisplayName = "GET das efetivas devolve lista vazia sem concessão, ainda que o token traga o papel mais amplo")]
    public async Task Efetivas_SemConcessao_ListaVazia()
    {
        using HttpClient client = Cliente(Betina, papeisDeRealm: "plataforma-admin");

        using HttpResponseMessage resposta = await client.GetAsync(new Uri(Efetivas, UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength().Should().Be(0);
    }

    // O token só identifica a pessoa; a permissão vem da concessão gravada do perfil que a reúne.
    private async Task<HttpClient> ClienteComConcessaoAsync(params string[] permissoes)
    {
        await ConcessoesDeTeste.ConcederPermissoesAsync(_fixture.Factory.Services, Leandro, permissoes);
        return Cliente(Leandro);
    }

    private HttpClient Cliente(string sub, string? papeisDeRealm = null)
    {
        HttpClient client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme,
            TestAuthHandler.TokenValue);
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, sub);
        client.DefaultRequestHeaders.Add(TestAuthHandler.NameHeader, "Pessoa de teste");
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, "pessoa@uniplus-test.local");
        if (papeisDeRealm is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, papeisDeRealm);
        }

        return client;
    }
}
