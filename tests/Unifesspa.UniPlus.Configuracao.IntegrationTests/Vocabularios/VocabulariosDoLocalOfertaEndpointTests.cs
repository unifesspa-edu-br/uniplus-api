namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Vocabularios;

using System.Net;
using System.Net.Http.Headers;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

/// <summary>
/// Contrato HTTP do vocabulário do local de oferta: os tipos de local, com código e rótulo literais, na ordem de
/// declaração, sem exigir autenticação.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class VocabulariosDoLocalOfertaEndpointTests
{
    private const string Rota = "/api/configuracao/vocabularios/tipos-local-oferta";

    private readonly ConfiguracaoEndpointFixture _fixture;

    public VocabulariosDoLocalOfertaEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    private sealed record ItemDoVocabulario(string Codigo, string Rotulo);

    [Fact(DisplayName = "GET /vocabularios/tipos-local-oferta devolve os seis tipos, na ordem de declaração, sem exigir autenticação")]
    public async Task ListarTiposLocalOferta_DevolveOsSeisTipos()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();

        ItemDoVocabulario[] itens = await ApoioDosVocabularios.LerAsync<ItemDoVocabulario>(
            client, "tipos-local-oferta", "codigo-tipo-local-oferta");

        itens.Should().Equal(
            new ItemDoVocabulario("CAMPUS_SEDE", "Campus sede"),
            new ItemDoVocabulario("CAMPUS_FORA_DE_SEDE", "Campus fora de sede"),
            new ItemDoVocabulario("CURSO_FORA_DE_SEDE", "Curso fora de sede"),
            new ItemDoVocabulario("POLO_DE_EDUCACAO_A_DISTANCIA", "Polo de educação a distância"),
            new ItemDoVocabulario("CONVENIO_DE_INTERIORIZACAO", "Convênio de interiorização"),
            new ItemDoVocabulario("OUTRO", "Outro"));
    }

    [Fact(DisplayName = "Versão inexistente da vendor MIME do vocabulário do local de oferta recusa com 406")]
    public async Task Vocabulario_VersaoDesconhecidaDaVendorMime_Recusa()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri(Rota, UriKind.Relative));
        request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/vnd.uniplus.codigo-tipo-local-oferta.v9+json"));

        using HttpResponseMessage resposta = await client.SendAsync(request);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotAcceptable);
    }
}
