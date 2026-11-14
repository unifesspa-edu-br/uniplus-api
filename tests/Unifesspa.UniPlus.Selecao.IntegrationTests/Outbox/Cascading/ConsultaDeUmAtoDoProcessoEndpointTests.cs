namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Services;

/// <summary>
/// A consulta de um ato devolve o mesmo ato que a lista do processo traz, e o ato de outro processo ou
/// inexistente responde igual, nomeado e sem revelar a existência.
/// </summary>
[Collection(CascadingCollection.Name)]
public sealed class ConsultaDeUmAtoDoProcessoEndpointTests
{
    private const string TipoDeMidiaDoAto = "application/vnd.uniplus.ato-do-processo.v1+json";

    private readonly CascadingFixture _fixture;

    public ConsultaDeUmAtoDoProcessoEndpointTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "O ato do processo responde 200 com o mesmo ato que a lista traz")]
    public async Task AtoDoProcesso_Responde200ComOMesmoDaLista()
    {
        (CascadingApiFactory api, HttpClient client) = await PrepararAsync();
        (Guid processoId, _) = await PublicarAtoAvulsoEndpointTests.PublicarEditalAsync(api, client);
        Guid atoId = await PublicarAvisoAsync(api, client, processoId);

        using HttpResponseMessage resposta = await ConsultarAsync(client, processoId, atoId);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        JsonElement ato = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        JsonElement daLista = (await PublicarAtoAvulsoEndpointTests.ConsultarAtosAsync(client, processoId))
            .Single(a => a.GetProperty("id").GetGuid() == atoId);
        ato.GetRawText().Should().Be(daLista.GetRawText());
        ato.GetProperty("tipoCodigo").GetString().Should().Be("AVISO");
    }

    [Fact(DisplayName = "O ato de outro processo responde 404 com o código do ato não encontrado")]
    public async Task AtoDeOutroProcesso_Responde404()
    {
        (CascadingApiFactory api, HttpClient client) = await PrepararAsync();
        (Guid processoDoAto, _) = await PublicarAtoAvulsoEndpointTests.PublicarEditalAsync(api, client);
        (Guid outroProcesso, _) = await PublicarAtoAvulsoEndpointTests.PublicarEditalAsync(api, client);
        Guid atoId = await PublicarAvisoAsync(api, client, processoDoAto);

        using HttpResponseMessage resposta = await ConsultarAsync(client, outroProcesso, atoId);

        await PublicarAtoAvulsoEndpointTests.ExigirProblemaAsync(resposta, "uniplus.selecao.ato_do_processo.nao_encontrado", HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "O ato inexistente responde 404 com o código do ato não encontrado")]
    public async Task AtoInexistente_Responde404()
    {
        (CascadingApiFactory api, HttpClient client) = await PrepararAsync();
        (Guid processoId, _) = await PublicarAtoAvulsoEndpointTests.PublicarEditalAsync(api, client);

        using HttpResponseMessage resposta = await ConsultarAsync(client, processoId, Guid.CreateVersion7());

        await PublicarAtoAvulsoEndpointTests.ExigirProblemaAsync(resposta, "uniplus.selecao.ato_do_processo.nao_encontrado", HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "O processo inexistente responde 404 com o código do processo não encontrado")]
    public async Task ProcessoInexistente_Responde404()
    {
        (_, HttpClient client) = await PrepararAsync();

        using HttpResponseMessage resposta = await ConsultarAsync(client, Guid.CreateVersion7(), Guid.CreateVersion7());

        await PublicarAtoAvulsoEndpointTests.ExigirProblemaAsync(resposta, "uniplus.selecao.processo_seletivo.nao_encontrado", HttpStatusCode.NotFound);
    }

    private async Task<(CascadingApiFactory Api, HttpClient Client)> PrepararAsync()
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        return (api, api.CreateClient());
    }

    private static async Task<Guid> PublicarAvisoAsync(CascadingApiFactory api, HttpClient client, Guid processoId)
    {
        Guid documento = await PublicarAtoAvulsoEndpointTests.SemearDocumentoAsync(api, processoId, TiposDeMidiaDeDocumento.Pdf, "aviso.pdf");
        using HttpResponseMessage resposta = await PublicarAtoAvulsoEndpointTests.PublicarAvisoAsync(client, processoId, documento);
        resposta.StatusCode.Should().Be(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync());
        return (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("atoId").GetGuid();
    }

    private static Task<HttpResponseMessage> ConsultarAsync(HttpClient client, Guid processoId, Guid atoId)
    {
        HttpRequestMessage pedido = PublicarAtoAvulsoEndpointTests.Autenticado(
            HttpMethod.Get, $"/api/selecao/processos-seletivos/{processoId}/atos/{atoId}");
        pedido.Headers.TryAddWithoutValidation("Accept", TipoDeMidiaDoAto);
        return client.SendAsync(pedido);
    }
}
