namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Instituicoes;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Infrastructure;

/// <summary>
/// A leitura pública da Instituição pelo host real, com Wolverine e Postgres efêmero: o DTO e os links
/// que o cliente recebe quando a Instituição existe.
/// </summary>
[Collection(OrganizacaoEndpointCollection.Name)]
public sealed class InstituicaoObterEndpointTests : IAsyncLifetime
{
    private readonly OrganizacaoEndpointFixture _fixture;

    public InstituicaoObterEndpointTests(OrganizacaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.RemoverInstituicoesAsync();

    public Task DisposeAsync() => _fixture.RemoverInstituicoesAsync();

    [Fact(DisplayName = "GET /api/organizacao/instituicao retorna 200 com o vendor MIME de instituição")]
    public async Task Obter_ComInstituicao_Retorna200ComVendorMime()
    {
        await _fixture.SemearInstituicaoAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(new Uri("/api/organizacao/instituicao", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/vnd.uniplus.instituicao.v1+json");
    }

    [Fact(DisplayName = "GET /api/organizacao/instituicao devolve o DTO da Instituição cadastrada")]
    public async Task Obter_ComInstituicao_DevolveODto()
    {
        Instituicao instituicao = await _fixture.SemearInstituicaoAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(new Uri("/api/organizacao/instituicao", UriKind.Relative));

        using JsonDocument corpo = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement raiz = corpo.RootElement;
        raiz.GetProperty("id").GetGuid().Should().Be(instituicao.Id);
        raiz.GetProperty("codigoEmec").GetString().Should().Be(instituicao.CodigoEmec);
        raiz.GetProperty("nome").GetString().Should().Be(instituicao.Nome);
        raiz.GetProperty("sigla").GetString().Should().Be(instituicao.Sigla);
        raiz.GetProperty("organizacaoAcademica").GetString().Should().Be(instituicao.OrganizacaoAcademica);
        raiz.GetProperty("categoriaAdministrativa").GetString().Should().Be(instituicao.CategoriaAdministrativa);
    }

    [Fact(DisplayName = "GET /api/organizacao/instituicao devolve em _links só o self, com a própria rota")]
    public async Task Obter_ComInstituicao_DevolveOSelfNosLinks()
    {
        await _fixture.SemearInstituicaoAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(new Uri("/api/organizacao/instituicao", UriKind.Relative));

        using JsonDocument corpo = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement links = corpo.RootElement.GetProperty("_links");
        links.EnumerateObject().Select(link => link.Name).Should().Equal("self");
        links.GetProperty("self").GetString().Should().Be("/api/organizacao/instituicao");
    }
}
