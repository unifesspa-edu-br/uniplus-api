namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Collections.Specialized;
using System.Net;
using System.Text.Json;
using System.Web;

using AwesomeAssertions;

using Xunit;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.PublicacaoPeloEndpointDeTeste;

/// <summary>
/// <b>O limite de 200 caracteres da busca da lista pública de certames não conta os espaços das pontas.</b>
/// </summary>
/// <remarks>
/// A busca é repetida nos links de continuação sem os espaços das pontas, e o cursor vale para a mesma busca,
/// escrita com ou sem esses espaços.
/// </remarks>
[Collection(CascadingCollection.Name)]
public sealed class BuscaSemOsEspacosDasPontasNaListaPublicaTests
{
    private const string Rota = "/api/selecao/certames";
    private const string LimiteDaBusca = "uniplus.consulta.busca_muito_longa";
    private const string CursorRecusado = "uniplus.paginacao.cursor_invalido";

    private readonly CascadingFixture _fixture;

    public BuscaSemOsEspacosDasPontasNaListaPublicaTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Busca com 200 caracteres úteis e espaços nas duas pontas é aceita")]
    public async Task Listar_BuscaCom200CaracteresUteisCercadosDeEspacos_EAceita()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await client.GetAsync(
            UriDaBusca($"  {new string('a', 200)}  "), CancellationToken.None);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "Busca com 201 caracteres úteis e espaços nas pontas é recusada pelo limite")]
    public async Task Listar_BuscaCom201CaracteresUteisCercadosDeEspacos_ERecusada()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await client.GetAsync(
            UriDaBusca($"  {new string('a', 201)}  "), CancellationToken.None);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resposta.Content.ReadAsStringAsync(CancellationToken.None)).Should().Contain(LimiteDaBusca);
    }

    [Fact(DisplayName = "O Link da página seguinte repete a busca sem os espaços das pontas")]
    public async Task Listar_BuscaCercadaDeEspacos_RepeteABuscaSemEspacosNoLink()
    {
        const string termo = "ContinuacaoPeloLink";
        using HttpClient client = _fixture.Factory.CreateClient();

        PaginaComContinuacao primeira = await ListarPrimeiraPaginaAsync(client, termo);

        BuscaDoLink(primeira.Continuacao).Should().Be(termo);
    }

    [Fact(DisplayName = "Seguir o Link de uma busca cercada de espaços devolve a página seguinte")]
    public async Task Listar_SeguindoOLinkDeBuscaCercadaDeEspacos_DevolveAPaginaSeguinte()
    {
        const string termo = "PaginaSeguinteDoLink";
        using HttpClient client = _fixture.Factory.CreateClient();
        PaginaComContinuacao primeira = await ListarPrimeiraPaginaAsync(client, termo);

        using HttpResponseMessage seguinte = await client.GetAsync(primeira.Continuacao, CancellationToken.None);

        seguinte.StatusCode.Should().Be(HttpStatusCode.OK);
        (await IdsAsync(seguinte)).Should().ContainSingle().Which.Should().NotBe(primeira.Id);
    }

    [Fact(DisplayName = "O cursor emitido para uma busca cercada de espaços continua com a mesma busca sem os espaços")]
    public async Task Listar_CursorDeBuscaCercadaDeEspacos_ContinuaComABuscaSemEspacos()
    {
        const string termo = "ContinuacaoSemOsEspacos";
        using HttpClient client = _fixture.Factory.CreateClient();
        PaginaComContinuacao primeira = await ListarPrimeiraPaginaAsync(client, termo);

        using HttpResponseMessage seguinte = await client.GetAsync(
            ComBusca(primeira.Continuacao, termo), CancellationToken.None);

        seguinte.StatusCode.Should().Be(HttpStatusCode.OK);
        (await IdsAsync(seguinte)).Should().ContainSingle().Which.Should().NotBe(primeira.Id);
    }

    [Fact(DisplayName = "O cursor emitido para uma busca é recusado quando continuado com outra busca")]
    public async Task Listar_CursorDeUmaBuscaContinuadoComOutra_ERecusado()
    {
        const string termo = "ContinuacaoComOutraBusca";
        using HttpClient client = _fixture.Factory.CreateClient();
        PaginaComContinuacao primeira = await ListarPrimeiraPaginaAsync(client, termo);

        using HttpResponseMessage resposta = await client.GetAsync(
            ComBusca(primeira.Continuacao, "outra"), CancellationToken.None);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await resposta.Content.ReadAsStringAsync(CancellationToken.None)).Should().Contain(CursorRecusado);
    }

    private async Task<PaginaComContinuacao> ListarPrimeiraPaginaAsync(HttpClient client, string termo)
    {
        await TiposDeAtoSeeder.SemearAsync(_fixture.Factory.Services);
        await PublicarCertameAsync(client, termo);
        await PublicarCertameAsync(client, termo);

        using HttpResponseMessage resposta = await client.GetAsync(UriDaBusca($" {termo} ", "&limit=1"), CancellationToken.None);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return new PaginaComContinuacao((await IdsAsync(resposta)).Single(), ProximaPagina(resposta));
    }

    private async Task PublicarCertameAsync(HttpClient client, string nome)
    {
        (Guid processoId, Guid documentoId) = await SemearProcessoAsync(_fixture.Factory, nome);

        using HttpResponseMessage publicacao = await PublicarAsync(client, processoId, documentoId);

        publicacao.StatusCode.Should().Be(HttpStatusCode.NoContent, await publicacao.Content.ReadAsStringAsync());
    }

    private static Uri UriDaBusca(string busca, string restante = "") =>
        new($"{Rota}?q={Uri.EscapeDataString(busca)}{restante}", UriKind.Relative);

    private static Uri ComBusca(Uri continuacao, string busca)
    {
        NameValueCollection parametros = HttpUtility.ParseQueryString(continuacao.Query);
        string cursor = Uri.EscapeDataString(parametros["cursor"]!);
        string direcao = Uri.EscapeDataString(parametros["direction"]!);

        return UriDaBusca(busca, $"&cursor={cursor}&direction={direcao}");
    }

    private static string? BuscaDoLink(Uri continuacao) =>
        HttpUtility.ParseQueryString(continuacao.Query)["q"];

    private static Uri ProximaPagina(HttpResponseMessage resposta)
    {
        string proxima = string.Join(",", resposta.Headers.GetValues("Link")).Split(',')
            .Select(static parte => parte.Trim())
            .Single(static parte => parte.EndsWith("rel=\"next\"", StringComparison.Ordinal));
        int inicio = proxima.IndexOf('<', StringComparison.Ordinal) + 1;

        return new Uri(proxima[inicio..proxima.IndexOf('>', StringComparison.Ordinal)], UriKind.Absolute);
    }

    private static async Task<List<Guid>> IdsAsync(HttpResponseMessage resposta)
    {
        using JsonDocument corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(CancellationToken.None));

        return [.. corpo.RootElement.EnumerateArray().Select(static item => item.GetProperty("processoSeletivoId").GetGuid())];
    }

    private sealed record PaginaComContinuacao(Guid Id, Uri Continuacao);
}
