namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Unidades;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Infrastructure;

/// <summary>
/// A listagem pública de Unidades pelo host real, com Wolverine e Postgres efêmero, quando a busca e o tipo
/// válidos chegam juntos.
/// </summary>
[Collection(OrganizacaoEndpointCollection.Name)]
public sealed class UnidadeListagemEndpointTests
{
    private readonly OrganizacaoEndpointFixture _fixture;

    public UnidadeListagemEndpointTests(OrganizacaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "GET /api/organizacao/unidades?q=&tipo= devolve só as Unidades da busca que são do tipo pedido")]
    public async Task Listar_ComBuscaETipoValidos_DevolveSoAsDoTipoPedido()
    {
        string marca = Guid.NewGuid().ToString("N")[..10];
        await _fixture.SemearUnidadeAsync($"{marca}c", TipoUnidade.Centro);
        await _fixture.SemearUnidadeAsync($"{marca}f", TipoUnidade.Faculdade);
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(
            new Uri($"/api/organizacao/unidades?q={marca}&tipo={(int)TipoUnidade.Centro}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await LerSiglasAsync(response)).Should().Equal($"{marca}c".ToUpperInvariant());
    }

    private static async Task<IReadOnlyList<string>> LerSiglasAsync(HttpResponseMessage response)
    {
        using JsonDocument corpo = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return [.. corpo.RootElement.EnumerateArray().Select(item => item.GetProperty("sigla").GetString()!)];
    }
}
