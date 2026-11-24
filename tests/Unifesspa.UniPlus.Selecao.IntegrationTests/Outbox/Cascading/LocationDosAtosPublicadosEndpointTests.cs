namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Services;

/// <summary>
/// O 201 dos dois endpoints de publicação traz o <c>Location</c> que a ADR-0025 exige: o endereço
/// onde o ato criado se consulta, e seguir esse endereço devolve o próprio ato.
/// </summary>
[Collection(CascadingCollection.Name)]
public sealed class LocationDosAtosPublicadosEndpointTests
{
    private readonly CascadingFixture _fixture;

    public LocationDosAtosPublicadosEndpointTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "A publicação do ato avulso responde 201 com o Location do ato criado, que a consulta devolve")]
    public async Task AtoAvulso_RespondeComLocationDoAtoCriado()
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        HttpClient client = api.CreateClient();
        (Guid processoId, _) = await PublicarAtoAvulsoEndpointTests.PublicarEditalAsync(api, client);
        Guid documento = await PublicarAtoAvulsoEndpointTests.SemearDocumentoAsync(api, processoId, TiposDeMidiaDeDocumento.Pdf, "aviso.pdf");

        using HttpResponseMessage resposta = await PublicarAtoAvulsoEndpointTests.PublicarAvisoAsync(client, processoId, documento);

        await ExigirLocationDoAtoAsync(resposta, processoId, client);
    }

    [Fact(DisplayName = "A publicação do item do edital responde 201 com o Location do ato criado, que a consulta devolve")]
    public async Task ItemDoEdital_RespondeComLocationDoAtoCriado()
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        HttpClient client = api.CreateClient();
        (Guid processoId, _) = await PublicarAtoAvulsoEndpointTests.PublicarEditalAsync(
            api, client, PublicarProdutoDoEditalEndpointTests.ProdutosDaHomologacao());
        Guid preliminar = await ProdutoPreliminarAsync(client, processoId);
        Guid documento = await PublicarAtoAvulsoEndpointTests.SemearDocumentoAsync(api, processoId, TiposDeMidiaDeDocumento.Pdf, "lista.pdf");

        using HttpResponseMessage resposta = await PublicarProdutoDoEditalEndpointTests.PublicarAsync(client, processoId, preliminar, documento);

        await ExigirLocationDoAtoAsync(resposta, processoId, client);
    }

    private static async Task<Guid> ProdutoPreliminarAsync(HttpClient client, Guid processoId) =>
        (await PublicarProdutoDoEditalEndpointTests.ConsultarItensAsync(client, processoId))
            .GetProperty("fases").EnumerateArray()
            .SelectMany(static f => f.GetProperty("produtos").EnumerateArray())
            .Single(static p => p.GetProperty("papel").GetProperty("codigo").GetString() == "PRELIMINAR")
            .GetProperty("id").GetGuid();

    private static async Task ExigirLocationDoAtoAsync(HttpResponseMessage resposta, Guid processoId, HttpClient client)
    {
        resposta.StatusCode.Should().Be(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync());
        Guid atoId = (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("atoId").GetGuid();
        resposta.Headers.Location.Should().NotBeNull();
        resposta.Headers.Location!.AbsolutePath.Should().Be($"/api/selecao/processos-seletivos/{processoId}/atos/{atoId}");

        using HttpRequestMessage consulta = PublicarAtoAvulsoEndpointTests.Autenticado(HttpMethod.Get, resposta.Headers.Location.AbsolutePath);
        consulta.Headers.TryAddWithoutValidation("Accept", "application/vnd.uniplus.ato-do-processo.v1+json");
        using HttpResponseMessage ato = await client.SendAsync(consulta);

        ato.StatusCode.Should().Be(HttpStatusCode.OK, await ato.Content.ReadAsStringAsync());
        (await ato.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid().Should().Be(atoId);
    }
}
