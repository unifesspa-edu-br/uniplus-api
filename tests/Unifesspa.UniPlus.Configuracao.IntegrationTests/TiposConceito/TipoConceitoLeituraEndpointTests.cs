namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposConceito;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure.TiposConceitoHttpDeTeste;

/// <summary>Leitura pública do catálogo de tipos de conceito: só os ativos, paginados por cursor, e a leitura por id.</summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class TipoConceitoLeituraEndpointTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public TipoConceitoLeituraEndpointTests(ConfiguracaoEndpointFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "GET /api/configuracao/tipos-conceito é público e retorna o vendor MIME")]
    public async Task Listar_Publico_Retorna200ComVendorMime()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();

        using HttpResponseMessage resposta = await client.GetAsync(new Uri(Publico, UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        resposta.Content.Headers.ContentType?.MediaType.Should().Be("application/vnd.uniplus.tipo-conceito.v1+json");
    }

    [Fact(DisplayName = "A listagem pública devolve só os tipos ativos")]
    public async Task Listar_OmiteOsDesativados()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid ativo = await CriarAsync(client, Numerico());
        Guid desativado = await CriarAsync(client, Categorico());
        (await DesativarAsync(client, desativado)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        IReadOnlyList<Guid> ids = await IdsAtivosAsync(client, limite: 50);

        ids.Should().Contain(ativo).And.NotContain(desativado);
    }

    [Fact(DisplayName = "O cursor percorre a listagem sem repetir nem perder tipo ativo")]
    public async Task Listar_ComCursor_PercorreCadaTipoUmaVez()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid[] criados = [await CriarAsync(client, Numerico()), await CriarAsync(client, Categorico()), await CriarAsync(client, Numerico())];

        IReadOnlyList<Guid> percorridos = await IdsAtivosAsync(client, limite: 2);

        percorridos.Should().OnlyHaveUniqueItems();
        percorridos.Should().Contain(criados);
    }

    [Fact(DisplayName = "A página limitada a dois itens traz o link da próxima, que não repete os itens da primeira")]
    public async Task Listar_ComLimite_ExpoeALigacaoDaProximaPagina()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        await CriarAsync(client, Numerico());
        await CriarAsync(client, Numerico());
        await CriarAsync(client, Numerico());

        using HttpResponseMessage primeira = await client.GetAsync(new Uri($"{Publico}?limit=2", UriKind.Relative));
        IReadOnlyList<Guid> daPrimeira = await IdsDaPaginaAsync(primeira);
        string proxima = CampusHttpDeTeste.LinkRel(primeira, "next").Should().NotBeNull().And.Subject.As<string>();
        using HttpResponseMessage segunda = await client.GetAsync(new Uri(proxima, UriKind.RelativeOrAbsolute));

        daPrimeira.Should().HaveCount(2);
        (await IdsDaPaginaAsync(segunda)).Should().NotBeEmpty().And.NotContain(daPrimeira);
    }

    private async Task<JsonDocument> ObterNumericoCriadoAsync(string codigo)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarAsync(client, Numerico(codigo));

        using HttpResponseMessage resposta = await ObterAsync(client, id);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return await LerAsync(resposta);
    }

    [Fact(DisplayName = "GET por id devolve a identidade do tipo numérico, o estado ativo e os links")]
    public async Task Obter_TipoNumerico_DevolveAIdentidadeEOsLinks()
    {
        string codigo = CodigoUnico();

        using JsonDocument tipo = await ObterNumericoCriadoAsync(codigo);

        JsonElement raiz = tipo.RootElement;
        (raiz.GetProperty("codigo").GetString(), raiz.GetProperty("ativo").GetBoolean()).Should().Be((codigo, true));
        raiz.TryGetProperty("_links", out _).Should().BeTrue("a leitura pública expõe HATEOAS nível 1");
    }

    [Fact(DisplayName = "GET por id devolve a definição do tipo numérico com a escala e sem valores")]
    public async Task Obter_TipoNumerico_DevolveAEscala()
    {
        using JsonDocument tipo = await ObterNumericoCriadoAsync(CodigoUnico());

        JsonElement definicao = tipo.RootElement.GetProperty("definicao");
        definicao.GetProperty("natureza").GetProperty("codigo").GetString().Should().Be("NUMERICO");
        definicao.GetProperty("numerico").GetProperty("escalaMaxima").GetDecimal().Should().Be(10m);
        Ausente(definicao, "valores").Should().BeTrue("o tipo numérico não tem valores categóricos");
    }

    [Fact(DisplayName = "GET por id devolve o tipo categórico com os valores na ordem informada, sem escala")]
    public async Task Obter_TipoCategorico_DevolveOsValoresEmOrdem()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarAsync(client, Categorico());

        using HttpResponseMessage resposta = await ObterAsync(client, id);

        using JsonDocument tipo = await LerAsync(resposta);
        JsonElement definicao = tipo.RootElement.GetProperty("definicao");
        definicao.GetProperty("valores").EnumerateArray().Select(valor => valor.GetProperty("codigo").GetString())
            .Should().Equal("INDEFERIDO", "DEFERIDO");
        definicao.GetProperty("valores")[1].GetProperty("contaComoAprovacao").GetBoolean().Should().BeTrue();
        Ausente(definicao, "numerico").Should().BeTrue("o tipo categórico não tem escala");
    }

    [Fact(DisplayName = "GET por id inexistente retorna 404")]
    public async Task Obter_Inexistente_Retorna404()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();

        using HttpResponseMessage resposta = await ObterAsync(client, Guid.CreateVersion7());

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
