namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Unidades;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Infrastructure;

/// <summary>
/// A leitura pública de uma Unidade pelo Id, pelo host real, com Wolverine e Postgres efêmero: o DTO e os
/// links que o cliente recebe quando a Unidade existe.
/// </summary>
[Collection(OrganizacaoEndpointCollection.Name)]
public sealed class UnidadeObterEndpointTests
{
    private readonly OrganizacaoEndpointFixture _fixture;

    public UnidadeObterEndpointTests(OrganizacaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "GET /api/organizacao/unidades/{id} retorna 200 com o vendor MIME de unidade")]
    public async Task ObterPorId_Existente_Retorna200ComVendorMime()
    {
        Unidade unidade = await _fixture.SemearUnidadeAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(RotaDe(unidade));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/vnd.uniplus.unidade.v1+json");
    }

    [Fact(DisplayName = "GET /api/organizacao/unidades/{id} devolve o DTO da Unidade cadastrada")]
    public async Task ObterPorId_Existente_DevolveODto()
    {
        Unidade unidade = await _fixture.SemearUnidadeAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(RotaDe(unidade));

        using JsonDocument corpo = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement raiz = corpo.RootElement;
        raiz.GetProperty("id").GetGuid().Should().Be(unidade.Id);
        raiz.GetProperty("nome").GetString().Should().Be(unidade.Nome);
        raiz.GetProperty("slug").GetString().Should().Be(unidade.Slug.Valor);
        raiz.GetProperty("sigla").GetString().Should().Be(unidade.Sigla);
        raiz.GetProperty("codigo").GetString().Should().Be(unidade.Codigo);
        raiz.GetProperty("tipo").GetString().Should().Be("CENTRO");
    }

    [Fact(DisplayName = "GET /api/organizacao/unidades/{id} devolve em _links o self e a collection")]
    public async Task ObterPorId_Existente_DevolveSelfECollectionNosLinks()
    {
        Unidade unidade = await _fixture.SemearUnidadeAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(RotaDe(unidade));

        using JsonDocument corpo = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement links = corpo.RootElement.GetProperty("_links");
        links.EnumerateObject().Select(link => link.Name).Should().BeEquivalentTo(["self", "collection"]);
        links.GetProperty("self").GetString().Should().Be($"/api/organizacao/unidades/{unidade.Id}");
        links.GetProperty("collection").GetString().Should().Be("/api/organizacao/unidades");
    }

    private static Uri RotaDe(Unidade unidade) => new($"/api/organizacao/unidades/{unidade.Id}", UriKind.Relative);
}
