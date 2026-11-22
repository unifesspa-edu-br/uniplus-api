namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Vocabularios;

using System.Net;
using System.Net.Http.Headers;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

/// <summary>
/// Contrato HTTP do vocabulário do termo de consentimento: as formas de aceite, com código e rótulo literais, na ordem
/// de declaração, sem exigir autenticação.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class VocabulariosDaFormaAceiteEndpointTests
{
    private const string Rota = "/api/configuracao/vocabularios/formas-aceite";

    private readonly ConfiguracaoEndpointFixture _fixture;

    public VocabulariosDaFormaAceiteEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    private sealed record ItemDoVocabulario(string Codigo, string Rotulo);

    [Fact(DisplayName = "GET /vocabularios/formas-aceite devolve as três formas, na ordem de declaração, sem exigir autenticação")]
    public async Task ListarFormasAceite_DevolveAsTresFormas()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();

        ItemDoVocabulario[] itens = await ApoioDosVocabularios.LerAsync<ItemDoVocabulario>(
            client, "formas-aceite", "codigo-forma-aceite");

        itens.Should().Equal(
            new ItemDoVocabulario("A_DEFINIR", "A definir"),
            new ItemDoVocabulario("REGISTRO_DIGITAL_SEM_LOG_IP", "Registro digital sem log do endereço IP"),
            new ItemDoVocabulario("REGISTRO_DIGITAL_COM_LOG_IP", "Registro digital com log do endereço IP"));
    }

    [Fact(DisplayName = "Versão inexistente da vendor MIME do vocabulário da forma de aceite recusa com 406")]
    public async Task Vocabulario_VersaoDesconhecidaDaVendorMime_Recusa()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri(Rota, UriKind.Relative));
        request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/vnd.uniplus.codigo-forma-aceite.v9+json"));

        using HttpResponseMessage resposta = await client.SendAsync(request);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotAcceptable);
    }
}
