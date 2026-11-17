namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.TiposAtoPublicado;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.Testing;

using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;

using static TipoAtoPublicadoApoio;

/// <summary>
/// "Hoje" nas leituras públicas do catálogo de tipos de ato é o dia civil no fuso institucional. Às
/// 22h30 em Belém já é o dia seguinte em UTC, e as versões que começam ou terminam nesse dia
/// seguinte mostram de que lado da borda a leitura se coloca.
/// </summary>
[Collection(PublicacoesEndpointCollection.Name)]
public sealed class TipoAtoPublicadoDiaCivilEndpointTests
{
    private const string CodigoDoFusoNaoReconhecido = "uniplus.selecao.processo_seletivo.fuso_institucional_nao_reconhecido";
    private const string DiaSeguinteAoDeBelem = "2026-06-16";

    private readonly PublicacoesEndpointFixture _fixture;
    private readonly TipoAtoPublicadoApoio _apoio;

    public TipoAtoPublicadoDiaCivilEndpointTests(PublicacoesEndpointFixture fixture)
    {
        _fixture = fixture;
        _apoio = new TipoAtoPublicadoApoio(fixture);
    }

    [Fact(DisplayName = "A listagem às 22h30 em Belém não traz a versão que só começa no dia seguinte em UTC")]
    public async Task Listar_VersaoQueComecaNoDiaSeguinteEmUtc_NaoApareceEmBelem()
    {
        Guid versao = await _apoio.CriarAsync(CodigoUnico(), inicio: DiaSeguinteAoDeBelem);
        using WebApplicationFactory<HostAssemblyMarker> host = _fixture.Factory.ComRelogioEm(AjustesDoHostDeTeste.VinteEDuasEMeiaEmBelem);
        using HttpClient client = host.CreateClient();

        (await IdsAsync(client, $"{Base}?limit=100")).Should().NotContain(versao);
    }

    [Fact(DisplayName = "A listagem às 22h30 em Belém traz a versão que termina no dia seguinte em UTC")]
    public async Task Listar_VersaoComFimNoDiaSeguinteEmUtc_ApareceEmBelem()
    {
        Guid versao = await _apoio.CriarAsync(CodigoUnico(), fim: DiaSeguinteAoDeBelem);
        using WebApplicationFactory<HostAssemblyMarker> host = _fixture.Factory.ComRelogioEm(AjustesDoHostDeTeste.VinteEDuasEMeiaEmBelem);
        using HttpClient client = host.CreateClient();

        (await IdsAsync(client, $"{Base}?limit=100")).Should().Contain(versao);
    }

    [Fact(DisplayName = "A listagem com vigência e o fuso não reconhecido responde 500 com o código do fuso")]
    public async Task Listar_ComVigenciaEFusoNaoReconhecido_Retorna500ComCodigoDoFuso()
    {
        using WebApplicationFactory<HostAssemblyMarker> host = _fixture.Factory.ComFusoNaoReconhecido();
        using HttpClient client = host.CreateClient();

        HttpResponseMessage resposta = await client.GetAsync(new Uri(Base, UriKind.Relative));

        await ConferirRecusaDoFusoAsync(resposta);
    }

    [Fact(DisplayName = "A listagem da série histórica não depende do fuso e responde 200 mesmo sem ele")]
    public async Task Listar_SemFiltroDeVigenciaEFusoNaoReconhecido_Retorna200()
    {
        using WebApplicationFactory<HostAssemblyMarker> host = _fixture.Factory.ComFusoNaoReconhecido();
        using HttpClient client = host.CreateClient();

        HttpResponseMessage resposta = await client.GetAsync(new Uri($"{Base}?vigentes=false", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "O cursor malformado é recusado com 400 antes de o fuso ser consultado")]
    public async Task Listar_CursorMalformadoEFusoNaoReconhecido_Retorna400()
    {
        using WebApplicationFactory<HostAssemblyMarker> host = _fixture.Factory.ComFusoNaoReconhecido();
        using HttpClient client = host.CreateClient();

        HttpResponseMessage resposta = await client.GetAsync(
            new Uri($"{Base}?cursor=isto-nao-e-um-cursor", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "A versão vigente sem data às 22h30 em Belém é a que termina no dia seguinte em UTC")]
    public async Task ObterVigente_SemDataEVersaoComFimNoDiaSeguinteEmUtc_ResolveEmBelem()
    {
        string codigo = CodigoUnico();
        Guid versao = await _apoio.CriarAsync(codigo, fim: DiaSeguinteAoDeBelem);
        using WebApplicationFactory<HostAssemblyMarker> host = _fixture.Factory.ComRelogioEm(AjustesDoHostDeTeste.VinteEDuasEMeiaEmBelem);
        using HttpClient client = host.CreateClient();

        using JsonDocument documento = await LerAsync(client, new Uri($"{Base}/{codigo}/vigente", UriKind.Relative));

        documento.RootElement.GetProperty("id").GetGuid().Should().Be(versao);
    }

    [Fact(DisplayName = "A versão vigente sem data e o fuso não reconhecido responde 500 com o código do fuso")]
    public async Task ObterVigente_SemDataEFusoNaoReconhecido_Retorna500ComCodigoDoFuso()
    {
        using WebApplicationFactory<HostAssemblyMarker> host = _fixture.Factory.ComFusoNaoReconhecido();
        using HttpClient client = host.CreateClient();

        HttpResponseMessage resposta = await client.GetAsync(new Uri($"{Base}/{CodigoUnico()}/vigente", UriKind.Relative));

        await ConferirRecusaDoFusoAsync(resposta);
    }

    [Fact(DisplayName = "A versão vigente numa data informada não depende do fuso")]
    public async Task ObterVigente_ComDataEFusoNaoReconhecido_Retorna200()
    {
        string codigo = CodigoUnico();
        await _apoio.CriarAsync(codigo);
        using WebApplicationFactory<HostAssemblyMarker> host = _fixture.Factory.ComFusoNaoReconhecido();
        using HttpClient client = host.CreateClient();

        HttpResponseMessage resposta = await client.GetAsync(
            new Uri($"{Base}/{codigo}/vigente?data=2026-03-15", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "O código malformado é recusado com 400 antes de o fuso ser consultado")]
    public async Task ObterVigente_CodigoMalformadoEFusoNaoReconhecido_Retorna400()
    {
        using WebApplicationFactory<HostAssemblyMarker> host = _fixture.Factory.ComFusoNaoReconhecido();
        using HttpClient client = host.CreateClient();

        HttpResponseMessage resposta = await client.GetAsync(new Uri($"{Base}/edital_abertura/vigente", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static async Task ConferirRecusaDoFusoAsync(HttpResponseMessage resposta)
    {
        resposta.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        using JsonDocument corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        corpo.RootElement.GetProperty("code").GetString().Should().Be(CodigoDoFusoNaoReconhecido);
    }

    private static async Task<IReadOnlyList<Guid>> IdsAsync(HttpClient client, string url)
    {
        List<Guid> ids = [];
        string? endereco = url;

        while (endereco is not null)
        {
            using HttpResponseMessage resposta = await client.GetAsync(new Uri(endereco, UriKind.RelativeOrAbsolute));
            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            using JsonDocument documento = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
            ids.AddRange(documento.RootElement.EnumerateArray().Select(item => item.GetProperty("id").GetGuid()));
            endereco = EnderecoDaProximaPagina(resposta);
        }

        return ids;
    }

    private static string? EnderecoDaProximaPagina(HttpResponseMessage resposta)
    {
        if (!resposta.Headers.TryGetValues("Link", out IEnumerable<string>? valores))
        {
            return null;
        }

        string? proxima = string.Join(',', valores).Split(',')
            .FirstOrDefault(static ligacao => ligacao.Contains("rel=\"next\"", StringComparison.Ordinal));

        return proxima is null ? null : proxima[(proxima.IndexOf('<', StringComparison.Ordinal) + 1)..proxima.IndexOf('>', StringComparison.Ordinal)];
    }
}
