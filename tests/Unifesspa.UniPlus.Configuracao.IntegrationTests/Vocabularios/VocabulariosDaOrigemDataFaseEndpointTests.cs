namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Vocabularios;

using System.Net;
using System.Net.Http.Headers;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

/// <summary>
/// Contrato HTTP do vocabulário da fase canônica: as origens da data da fase, com código e rótulo literais, na ordem de
/// declaração, sem exigir autenticação.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class VocabulariosDaOrigemDataFaseEndpointTests
{
    private const string Rota = "/api/configuracao/vocabularios/origens-data-fase";

    private readonly ConfiguracaoEndpointFixture _fixture;

    public VocabulariosDaOrigemDataFaseEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    private sealed record ItemDoVocabulario(string Codigo, string Rotulo);

    [Fact(DisplayName = "GET /vocabularios/origens-data-fase devolve as duas origens, na ordem de declaração, sem exigir autenticação")]
    public async Task ListarOrigensDataFase_DevolveAsDuasOrigens()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();

        ItemDoVocabulario[] itens = await ApoioDosVocabularios.LerAsync<ItemDoVocabulario>(
            client, "origens-data-fase", "codigo-origem-data-fase");

        itens.Should().Equal(
            new ItemDoVocabulario("NO_EDITAL", "No edital"),
            new ItemDoVocabulario("NA_EXECUCAO", "Na execução do processo"));
    }

    [Fact(DisplayName = "Versão inexistente da vendor MIME do vocabulário da origem da data da fase recusa com 406")]
    public async Task Vocabulario_VersaoDesconhecidaDaVendorMime_Recusa()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri(Rota, UriKind.Relative));
        request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/vnd.uniplus.codigo-origem-data-fase.v9+json"));

        using HttpResponseMessage resposta = await client.SendAsync(request);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotAcceptable);
    }
}
