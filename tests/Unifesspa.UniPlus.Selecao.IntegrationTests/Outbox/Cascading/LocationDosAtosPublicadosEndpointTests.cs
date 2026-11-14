namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Services;

/// <summary>
/// O 201 dos dois endpoints de publicação traz o <c>Location</c> que a ADR-0025 exige: o endereço
/// onde o ato criado se consulta, a lista dos atos do processo, que é a única leitura administrativa
/// do ato.
/// </summary>
[Collection(CascadingCollection.Name)]
public sealed class LocationDosAtosPublicadosEndpointTests
{
    private readonly CascadingFixture _fixture;

    public LocationDosAtosPublicadosEndpointTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "A publicação do ato avulso responde 201 com o Location da lista de atos do processo")]
    public async Task AtoAvulso_RespondeComLocationDaListaDeAtos()
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        HttpClient client = api.CreateClient();
        (Guid processoId, _) = await PublicarAtoAvulsoEndpointTests.PublicarEditalAsync(api, client);
        Guid documento = await PublicarAtoAvulsoEndpointTests.SemearDocumentoAsync(api, processoId, TiposDeMidiaDeDocumento.Pdf, "aviso.pdf");

        using HttpResponseMessage resposta = await PublicarAtoAvulsoEndpointTests.PublicarAvisoAsync(client, processoId, documento);

        await ExigirLocationDaListaAsync(resposta, processoId);
    }

    [Fact(DisplayName = "A publicação do item do edital responde 201 com o Location da lista de atos do processo")]
    public async Task ItemDoEdital_RespondeComLocationDaListaDeAtos()
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        HttpClient client = api.CreateClient();
        (Guid processoId, _) = await PublicarAtoAvulsoEndpointTests.PublicarEditalAsync(
            api, client, PublicarProdutoDoEditalEndpointTests.ProdutosDaHomologacao());
        Guid preliminar = (await PublicarProdutoDoEditalEndpointTests.ConsultarItensAsync(client, processoId))
            .GetProperty("fases").EnumerateArray()
            .SelectMany(static f => f.GetProperty("produtos").EnumerateArray())
            .Single(static p => p.GetProperty("papel").GetString() == "PRELIMINAR")
            .GetProperty("id").GetGuid();
        Guid documento = await PublicarAtoAvulsoEndpointTests.SemearDocumentoAsync(api, processoId, TiposDeMidiaDeDocumento.Pdf, "lista.pdf");

        using HttpResponseMessage resposta = await PublicarProdutoDoEditalEndpointTests.PublicarAsync(client, processoId, preliminar, documento);

        await ExigirLocationDaListaAsync(resposta, processoId);
    }

    private static async Task ExigirLocationDaListaAsync(HttpResponseMessage resposta, Guid processoId)
    {
        resposta.StatusCode.Should().Be(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync());
        resposta.Headers.Location.Should().NotBeNull();
        resposta.Headers.Location!.AbsolutePath.Should().Be($"/api/selecao/processos-seletivos/{processoId}/atos");
    }
}
