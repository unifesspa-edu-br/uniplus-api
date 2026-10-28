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
using Unifesspa.UniPlus.Selecao.API.Permissoes;

/// <summary>
/// O catálogo de permissões montado pelo composition root real e os dois endpoints
/// que o consultam (ADR-0151).
/// </summary>
[Collection(MonolitoHostCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class CatalogoDePermissoesNoHostTests
{
    private const string Catalogo = "/api/autorizacao/permissoes";
    private const string Efetivas = "/api/autorizacao/permissoes/efetivas";

    private const string CodigoDoCatalogo = "autorizacao:permissoes:consultar-catalogo";

    private readonly MonolitoPostgresFixture _fixture;

    public CatalogoDePermissoesNoHostTests(MonolitoPostgresFixture fixture)
    {
        _fixture = fixture;
    }

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

    [Fact(DisplayName = "Os códigos existentes continuam sendo os nomes dos papéis do client no Keycloak")]
    public void Codigos_SaoOsNomesDosPapeis()
    {
        CatalogoDaAplicacao catalogo = _fixture.Factory.Services.GetRequiredService<CatalogoDaAplicacao>();

        catalogo.Definicoes.Select(static d => d.Codigo).Should().Contain(
        [
            "selecao:motivos-decisao-isencao:manter",
            "autorizacao:permissoes:consultar-catalogo",
        ]);
    }

    [Fact(DisplayName = "Toda verificação de contexto declarada no catálogo tem implementação")]
    public void Verificacoes_DeclaradasTemImplementacao()
    {
        using IServiceScope escopo = _fixture.Factory.Services.CreateScope();
        CatalogoDaAplicacao catalogo = escopo.ServiceProvider.GetRequiredService<CatalogoDaAplicacao>();

        string[] implementadas = [.. typeof(GrantEfetivoAplicavelCheck).Assembly.GetTypes()
            .Where(static t => t is { IsAbstract: false, IsInterface: false } && typeof(IPermissaoCheck).IsAssignableFrom(t))
            .Select(static t => ((IPermissaoCheck)Activator.CreateInstance(t)!).Nome)];

        catalogo.Definicoes.SelectMany(static d => d.Requisito.VerificacoesDeContexto)
            .Should().BeSubsetOf(implementadas)
            .And.NotContain(GrantEfetivoAplicavelCheck.NomeCanonico);
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
        using HttpClient client = ClienteComPermissoes(PermissaoSelecao.MotivosDecisaoIsencaoManter.Codigo);

        using HttpResponseMessage resposta = await client.GetAsync(new Uri(Catalogo, UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "GET do catálogo com a permissão lista as permissões agrupadas por módulo e recurso")]
    public async Task Catalogo_ComPermissao_ListaAgrupada()
    {
        using HttpClient client = ClienteComPermissoes(CodigoDoCatalogo);

        using HttpResponseMessage resposta = await client.GetAsync(new Uri(Catalogo, UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.uniplus.permissao-catalogo.v1+json");

        JsonElement corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        JsonElement selecao = corpo.EnumerateArray().Single(m => m.GetProperty("modulo").GetString() == "selecao");
        JsonElement recurso = selecao.GetProperty("recursos").EnumerateArray()
            .Single(r => r.GetProperty("recurso").GetString() == "motivos-decisao-isencao");

        recurso.GetProperty("permissoes").EnumerateArray()
            .Select(p => p.GetProperty("codigo").GetString())
            .Should().BeEquivalentTo("selecao:motivos-decisao-isencao:manter");
        recurso.GetProperty("permissoes")[0].GetProperty("nome").GetString().Should().NotBeNullOrWhiteSpace();
        recurso.GetProperty("permissoes")[0].GetProperty("sensibilidade").GetString().Should().Be("interna");
    }

    [Fact(DisplayName = "GET do catálogo com Accept de outro tipo responde 406")]
    public async Task Catalogo_AcceptIncompativel_406()
    {
        using HttpClient client = ClienteComPermissoes(CodigoDoCatalogo);
        client.DefaultRequestHeaders.Accept.ParseAdd("text/csv");

        using HttpResponseMessage resposta = await client.GetAsync(new Uri(Catalogo, UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.NotAcceptable);
    }

    [Fact(DisplayName = "Versão não suportada responde 406 com o código canônico e as versões servidas")]
    public async Task Catalogo_VersaoNaoSuportada_406CanonicoComVersoes()
    {
        using HttpClient client = ClienteComPermissoes(CodigoDoCatalogo);
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
        using HttpClient client = ClienteComPermissoes(CodigoDoCatalogo);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.uniplus.permissao-catalogo.v1+json;q=0, */*;q=1");

        using HttpResponseMessage resposta = await client.GetAsync(new Uri(Catalogo, UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.NotAcceptable);
    }

    [Fact(DisplayName = "Faixa específica aceita prevalece sobre curinga excluído")]
    public async Task Catalogo_FaixaEspecificaComCuringaExcluido_200()
    {
        using HttpClient client = ClienteComPermissoes(CodigoDoCatalogo);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.uniplus.permissao-catalogo.v1+json;q=1, */*;q=0");

        using HttpResponseMessage resposta = await client.GetAsync(new Uri(Catalogo, UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "GET das efetivas sem token responde 401")]
    public async Task Efetivas_SemToken_401()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await client.GetAsync(new Uri(Efetivas, UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "GET das efetivas devolve exatamente as permissões que o token concede")]
    public async Task Efetivas_DevolveSoAsConcedidas()
    {
        using HttpClient client = ClienteComPermissoes(
            PermissaoSelecao.MotivosDecisaoIsencaoManter.Codigo,
            "papel-que-nao-e-permissao");

        using HttpResponseMessage resposta = await client.GetAsync(new Uri(Efetivas, UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.uniplus.permissao-efetiva.v1+json");

        JsonElement corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        corpo.GetArrayLength().Should().Be(1);
        corpo[0].GetProperty("codigo").GetString().Should().Be("selecao:motivos-decisao-isencao:manter");
        corpo[0].GetProperty("nome").GetString().Should().Be("Manter motivos de decisão de isenção");
    }

    [Fact(DisplayName = "GET das efetivas devolve lista vazia quando o token não concede nenhuma")]
    public async Task Efetivas_SemConcessao_ListaVazia()
    {
        using HttpClient client = ClienteComPermissoes();

        using HttpResponseMessage resposta = await client.GetAsync(new Uri(Efetivas, UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength().Should().Be(0);
    }

    private HttpClient ClienteComPermissoes(params string[] permissoes)
    {
        HttpClient client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme,
            TestAuthHandler.TokenValue);
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, $"user-{Guid.NewGuid():N}"[..16]);
        client.DefaultRequestHeaders.Add(TestAuthHandler.NameHeader, "Usuario Teste");
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, "usuario@unifesspa.edu.br");
        client.DefaultRequestHeaders.Add(TestAuthHandler.PermissionsHeader, string.Join(',', permissoes));
        return client;
    }
}
