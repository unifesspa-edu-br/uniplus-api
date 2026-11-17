namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Unidades;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Assertions;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Infrastructure;

/// <summary>
/// A listagem pública de Unidades pelo host real, com Wolverine e Postgres efêmero: a vitrine, os filtros de
/// busca e de tipo, o header <c>Link</c> e a recusa da busca longa demais e do tipo inválido, na ordem em que
/// o handler as confere.
/// </summary>
[Collection(OrganizacaoEndpointCollection.Name)]
public sealed class UnidadeListagemEndpointTests
{
    private const string Rota = "/api/organizacao/unidades";
    private const string CodigoDaBuscaLonga = "uniplus.consulta.busca_muito_longa";
    private const string TituloDaBuscaLonga = "Texto pesquisado excede o comprimento aceito";
    private const string DetalheDaBuscaLonga = "O texto pesquisado excede 200 caracteres.";
    private const int Centro = (int)TipoUnidade.Centro;

    private readonly OrganizacaoEndpointFixture _fixture;

    public UnidadeListagemEndpointTests(OrganizacaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "GET /api/organizacao/unidades retorna 200 com Content-Type vendor MIME de unidade")]
    public async Task Listar_Retorna200ComVendorMime()
    {
        using HttpResponseMessage response = await ListarAsync(string.Empty);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/vnd.uniplus.unidade.v1+json");
    }

    [Fact(DisplayName = "GET /api/organizacao/unidades retorna array JSON")]
    public async Task Listar_RetornaArrayJson()
    {
        using HttpResponseMessage response = await ListarAsync(string.Empty);

        using JsonDocument corpo = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        corpo.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact(DisplayName = "GET /api/organizacao/unidades?q= filtra server-side por sigla/nome/código/slug/alias")]
    public async Task Listar_ComBusca_FiltraServerSide()
    {
        string marca = NovaMarca();
        await _fixture.SemearUnidadeAsync($"{marca}al", TipoUnidade.Centro);
        await _fixture.SemearUnidadeAsync(NovaMarca(), TipoUnidade.Faculdade);

        using HttpResponseMessage response = await ListarAsync($"?q={marca}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await LerSiglasAsync(response)).Should().Equal($"{marca}al".ToUpperInvariant());
    }

    [Fact(DisplayName = "GET /api/organizacao/unidades?q=&tipo= devolve só as Unidades da busca que são do tipo pedido")]
    public async Task Listar_ComBuscaETipoValidos_DevolveSoAsDoTipoPedido()
    {
        string marca = NovaMarca();
        await _fixture.SemearUnidadeAsync($"{marca}c", TipoUnidade.Centro);
        await _fixture.SemearUnidadeAsync($"{marca}f", TipoUnidade.Faculdade);

        using HttpResponseMessage response = await ListarAsync($"?q={marca}&tipo={Centro}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await LerSiglasAsync(response)).Should().Equal($"{marca}c".ToUpperInvariant());
    }

    [Fact(DisplayName = "GET /api/organizacao/unidades com tipo repetido devolve cada Unidade uma vez")]
    public async Task Listar_ComTipoRepetido_NaoRepeteAUnidade()
    {
        string marca = NovaMarca();
        await _fixture.SemearUnidadeAsync($"{marca}c", TipoUnidade.Centro);

        using HttpResponseMessage response = await ListarAsync($"?q={marca}&tipo={Centro}&tipo={Centro}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await LerSiglasAsync(response)).Should().Equal($"{marca}c".ToUpperInvariant());
    }

    [Fact(DisplayName = "GET /api/organizacao/unidades preserva q/tipo no header Link (self e next)")]
    public async Task Listar_ComFiltros_PreservaQueryParamsNoLink()
    {
        string marca = NovaMarca();
        await _fixture.SemearUnidadeAsync($"{marca}1", TipoUnidade.Centro);
        await _fixture.SemearUnidadeAsync($"{marca}2", TipoUnidade.Centro);

        using HttpResponseMessage response = await ListarAsync($"?q={marca}&tipo={Centro}&limit=1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        string link = response.Headers.GetValues("Link").Single();
        link.Should().Contain("rel=\"next\"", "limit=1 com 2 itens filtrados deve emitir próxima página");
        link.Should().Contain($"q={marca}").And.Contain($"tipo={Centro}", "os filtros viajam em self e next");
    }

    [Fact(DisplayName = "GET /api/organizacao/unidades?q= com 200 caracteres é aceito")]
    public async Task Listar_ComBuscaNoLimite_Retorna200()
    {
        using HttpResponseMessage response = await ListarAsync($"?q={new string('a', 200)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory(DisplayName = "GET /api/organizacao/unidades?q= acima de 200 caracteres retorna 422 com o código de busca muito longa")]
    [InlineData(201)]
    [InlineData(250)]
    public async Task Listar_ComBuscaAcimaDoLimite_Retorna422(int comprimento)
    {
        using HttpResponseMessage response = await ListarAsync($"?q={new string('a', comprimento)}");

        await ProblemaRegistrado.ConferirAsync(
            response, HttpStatusCode.UnprocessableEntity, CodigoDaBuscaLonga, TituloDaBuscaLonga, DetalheDaBuscaLonga);
    }

    [Fact(DisplayName = "GET /api/organizacao/unidades?tipo=<inválido> retorna 400 com o código do filtro de tipo e só o número")]
    public async Task Listar_ComTipoInvalido_Retorna400()
    {
        using HttpResponseMessage response = await ListarAsync("?tipo=999");

        await ProblemaRegistrado.ConferirAsync(
            response,
            HttpStatusCode.BadRequest,
            "uniplus.organizacao.unidade.filtro_de_tipo_invalido",
            "Tipo do filtro de unidades inválido",
            "O valor de tipo '999' não corresponde a um tipo de unidade válido.");
    }

    [Fact(DisplayName = "GET /api/organizacao/unidades com a busca longa e o tipo inválido juntos retorna o 422 da busca sem consultar o cadastro")]
    public async Task Listar_ComBuscaLongaETipoInvalido_RecusaABuscaSemConsultarOCadastro()
    {
        ConsultasAoCadastroContadas consultas = new();
        using HttpRequestMessage requisicao = new(
            HttpMethod.Get, new Uri($"{Rota}?q={new string('a', 201)}&tipo=999", UriKind.Relative));

        using HttpResponseMessage response = await _fixture.EnviarComCadastroContadoAsync(requisicao, consultas);

        await ProblemaRegistrado.ConferirAsync(
            response, HttpStatusCode.UnprocessableEntity, CodigoDaBuscaLonga, TituloDaBuscaLonga, DetalheDaBuscaLonga);
        consultas.Total.Should().Be(0);
    }

    [Fact(DisplayName = "GET /api/organizacao/unidades?q= com aparência de dado pessoal acima do limite não devolve o termo no corpo")]
    public async Task Listar_ComBuscaComAparenciaDeDadoPessoal_NaoDevolveOTermo()
    {
        const string emailFicticio = "pessoa@example.invalid";
        string busca = string.Concat(Enumerable.Repeat($"{emailFicticio} ", 15));

        using HttpResponseMessage response = await ListarAsync($"?q={Uri.EscapeDataString(busca)}");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync()).Should().NotContain(emailFicticio);
        await response.AssertNoPiiAsync();
    }

    private async Task<HttpResponseMessage> ListarAsync(string consulta)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        return await client.GetAsync(new Uri($"{Rota}{consulta}", UriKind.Relative));
    }

    private static string NovaMarca() => Guid.NewGuid().ToString("N")[..10];

    private static async Task<IReadOnlyList<string>> LerSiglasAsync(HttpResponseMessage response)
    {
        using JsonDocument corpo = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return [.. corpo.RootElement.EnumerateArray().Select(item => item.GetProperty("sigla").GetString()!)];
    }
}
