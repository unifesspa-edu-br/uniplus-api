namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TermosConsentimento;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// Smoke + caminho de escrita dos endpoints de <c>TermoConsentimento</c>
/// (UNI-REQ-0086, issue #1018): routing, vendor media type, HATEOAS,
/// autenticação/autorização, idempotência e o ciclo rascunho → revisão →
/// promoção, com Wolverine rodando contra Postgres efêmero.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class TermoConsentimentoEndpointTests
{
    private const string VendorMime = "application/vnd.uniplus.termo-consentimento.v1+json";
    private const string ColecaoPath = "/api/configuracao/termos-consentimento";
    private const string AdminPath = "/api/configuracao/admin/termos-consentimento";

    private readonly ConfiguracaoEndpointFixture _fixture;

    public TermoConsentimentoEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "GET coleção retorna 200 com Content-Type vendor MIME")]
    public async Task Listar_Retorna200ComVendorMime()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri(ColecaoPath, UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be(VendorMime);
    }

    [Fact(DisplayName = "GET por id retorna 404 quando inexistente")]
    public async Task ObterPorId_NaoExiste_Retorna404()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri($"{ColecaoPath}/{Guid.NewGuid()}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "POST admin sem autenticação retorna 401")]
    public async Task Criar_SemAuth_Retorna401()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(AdminPath, UriKind.Relative));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "POST admin autenticado sem role plataforma-admin retorna 403")]
    public async Task Criar_SemRoleAdmin_Retorna403()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(AdminPath, UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "candidato");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(CorpoValido());

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "POST admin cria (201 + Location) e o GET subsequente devolve o rascunho + _links")]
    public async Task Criar_ComAuthEIdempotency_Retorna201EPersiste()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarAdmin(client, HttpMethod.Post, AdminPath, CorpoValido());

        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        criar.Headers.Location.Should().NotBeNull();
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();
        id.Should().NotBe(Guid.Empty);

        HttpResponseMessage obter = await client.GetAsync(new Uri($"{ColecaoPath}/{id}", UriKind.Relative));
        obter.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument doc = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement;
        root.GetProperty("revisado").GetBoolean().Should().BeFalse("todo termo nasce EM_ELABORACAO");
        root.GetProperty("textoRascunho").GetString().Should().Be("Texto do termo");
        root.TryGetProperty("_links", out _).Should().BeTrue("HATEOAS Level 1 expõe _links.self (ADR-0029)");
    }

    [Fact(DisplayName = "GET coleção não inclui versoes — a listagem projeta só o resumo")]
    public async Task Listar_NaoIncluiVersoes()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarAdmin(client, HttpMethod.Post, AdminPath, CorpoValido());
        criar.StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage listar = await client.GetAsync(new Uri(ColecaoPath, UriKind.Relative));

        listar.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument doc = JsonDocument.Parse(await listar.Content.ReadAsStringAsync());
        JsonElement itens = doc.RootElement;
        itens.GetArrayLength().Should().BeGreaterThan(0);
        foreach (JsonElement item in itens.EnumerateArray())
        {
            item.TryGetProperty("versoes", out _).Should().BeFalse(
                "a listagem não carrega a coleção filha — use ObterPorId para o termo completo");
        }
    }

    [Fact(DisplayName = "Ciclo completo: editar rascunho → revisar → promover cria versão imutável")]
    public async Task CicloCompleto_EditarRevisarPromover_CriaVersao()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarTermo(client, CorpoVazio());

        HttpResponseMessage editar = await EnviarAdmin(
            client, HttpMethod.Put, $"{AdminPath}/{id}/rascunho",
            new { id, textoRascunho = "Texto do termo", baseLegalRascunho = "Lei 13.709/2018", formaAceiteRascunho = (string?)null });
        editar.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage revisar = await EnviarAdmin(client, HttpMethod.Post, $"{AdminPath}/{id}/revisar", null);
        revisar.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ObterRevisado(client, id)).Should().BeTrue();

        HttpResponseMessage promover = await EnviarAdmin(client, HttpMethod.Post, $"{AdminPath}/{id}/promover", null);
        promover.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage obter = await client.GetAsync(new Uri($"{ColecaoPath}/{id}", UriKind.Relative));
        using JsonDocument doc = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
        JsonElement versoes = doc.RootElement.GetProperty("versoes");
        versoes.GetArrayLength().Should().Be(1);

        // A revisão é consumida pela promoção — status volta a EM_ELABORACAO mesmo
        // sem nenhuma edição posterior do rascunho.
        doc.RootElement.GetProperty("revisado").GetBoolean().Should().BeFalse();

        // Identificador de autenticação do operador nunca sai na representação
        // pública (endpoint anônimo) — nem no termo, nem na versão promovida.
        doc.RootElement.TryGetProperty("revisadoPor", out _).Should().BeFalse();
        versoes[0].TryGetProperty("promovidaPor", out _).Should().BeFalse();
    }

    [Fact(DisplayName = "Editar rascunho já revisado reverte o status para EM_ELABORACAO")]
    public async Task EditarRascunho_TermoRevisado_ReverteStatus()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarTermo(client, CorpoValido());
        (await EnviarAdmin(client, HttpMethod.Post, $"{AdminPath}/{id}/revisar", null)).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
        (await ObterRevisado(client, id)).Should().BeTrue();

        HttpResponseMessage editar = await EnviarAdmin(
            client, HttpMethod.Put, $"{AdminPath}/{id}/rascunho",
            new { id, textoRascunho = "Texto ajustado", baseLegalRascunho = "Lei 13.709/2018", formaAceiteRascunho = (string?)null });
        editar.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ObterRevisado(client, id)).Should().BeFalse("editar o rascunho revisado reverte a marca de revisão");
    }

    [Fact(DisplayName = "Promover sem revisar retorna 422 com o código de erro PromocaoSemRevisao")]
    public async Task Promover_SemRevisar_Retorna422()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarTermo(client, CorpoValido());

        HttpResponseMessage promover = await EnviarAdmin(client, HttpMethod.Post, $"{AdminPath}/{id}/promover", null);

        promover.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await LerCodigoDeErro(promover)).Should().Be(
            "uniplus.configuracao.termo_consentimento.promocao_sem_revisao");
    }

    [Fact(DisplayName = "DELETE em termo com versão promovida retorna 409")]
    public async Task Remover_ComVersaoPromovida_Retorna409()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarTermo(client, CorpoValido());
        (await EnviarAdmin(client, HttpMethod.Post, $"{AdminPath}/{id}/revisar", null)).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
        (await EnviarAdmin(client, HttpMethod.Post, $"{AdminPath}/{id}/promover", null)).StatusCode
            .Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage response = await EnviarDeleteAdmin(client, id);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await LerCodigoDeErro(response)).Should().Be(
            "uniplus.configuracao.termo_consentimento.remocao_bloqueada_com_versao_promovida");
    }

    [Fact(DisplayName = "DELETE em termo sem versão promovida retorna 204")]
    public async Task Remover_SemVersaoPromovida_Retorna204()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarTermo(client, CorpoVazio());

        HttpResponseMessage response = await EnviarDeleteAdmin(client, id);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private static object CorpoValido() => new
    {
        nome = $"Termo LGPD {Guid.NewGuid():N}",
        textoRascunho = "Texto do termo",
        baseLegalRascunho = "Lei 13.709/2018",
        formaAceiteRascunho = "REGISTRO_DIGITAL_SEM_LOG_IP",
    };

    private static object CorpoVazio() => new
    {
        nome = $"Termo em branco {Guid.NewGuid():N}",
        textoRascunho = (string?)null,
        baseLegalRascunho = (string?)null,
        formaAceiteRascunho = (string?)null,
    };

    private static async Task<Guid> CriarTermo(HttpClient client, object corpo)
    {
        HttpResponseMessage criar = await EnviarAdmin(client, HttpMethod.Post, AdminPath, corpo);
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        return await criar.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task<bool> ObterRevisado(HttpClient client, Guid id)
    {
        HttpResponseMessage obter = await client.GetAsync(new Uri($"{ColecaoPath}/{id}", UriKind.Relative));
        using JsonDocument doc = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("revisado").GetBoolean();
    }

    private static async Task<string?> LerCodigoDeErro(HttpResponseMessage response)
    {
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("code").GetString();
    }

    private static async Task<HttpResponseMessage> EnviarAdmin(HttpClient client, HttpMethod method, string path, object? body)
    {
        using HttpRequestMessage request = new(method, new Uri(path, UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> EnviarDeleteAdmin(HttpClient client, Guid id)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Delete, new Uri($"{AdminPath}/{id}", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        return await client.SendAsync(request);
    }
}
