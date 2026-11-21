namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Vocabularios;

using System.Net;
using System.Net.Http.Headers;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

/// <summary>
/// Contrato HTTP dos vocabulários da oferta de curso: o programa de oferta, o formato pedagógico, os regimes de funcionamento e de turno e os turnos, com código e rótulo
/// literais, na ordem de declaração, sem exigir autenticação.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class VocabulariosDaOfertaDeCursoEndpointTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public VocabulariosDaOfertaDeCursoEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    private sealed record ItemDoVocabulario(string Codigo, string Rotulo);

    [Fact(DisplayName = "GET /vocabularios/programas-oferta devolve os sete programas, na ordem de declaração, sem exigir autenticação")]
    public async Task ListarProgramasDeOferta_DevolveOsSeteProgramas()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();

        ItemDoVocabulario[] itens = await ApoioDosVocabularios.LerAsync<ItemDoVocabulario>(
            client, "programas-oferta", "codigo-programa-de-oferta");

        itens.Should().Equal(
            new ItemDoVocabulario("REGULAR", "Regular"),
            new ItemDoVocabulario("FORMA_PARA", "Forma Pará"),
            new ItemDoVocabulario("PARFOR", "Parfor"),
            new ItemDoVocabulario("PRONERA", "Pronera"),
            new ItemDoVocabulario("PEPETI", "PEPETI"),
            new ItemDoVocabulario("CONVENIO_OUTRO", "Outro convênio"),
            new ItemDoVocabulario("OUTRO", "Outro programa"));
    }

    [Fact(DisplayName = "GET /vocabularios/formatos-pedagogicos devolve os três formatos, na ordem de declaração, sem exigir autenticação")]
    public async Task ListarFormatosPedagogicos_DevolveOsTresFormatos()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();

        ItemDoVocabulario[] itens = await ApoioDosVocabularios.LerAsync<ItemDoVocabulario>(
            client, "formatos-pedagogicos", "codigo-formato-pedagogico");

        itens.Should().Equal(
            new ItemDoVocabulario("PRESENCIAL", "Presencial"),
            new ItemDoVocabulario("SEMIPRESENCIAL", "Semipresencial"),
            new ItemDoVocabulario("EAD", "A distância"));
    }

    [Fact(DisplayName = "GET /vocabularios/regimes-funcionamento devolve os três regimes, na ordem de declaração, sem exigir autenticação")]
    public async Task ListarRegimesDeFuncionamento_DevolveOsTresRegimes()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();

        ItemDoVocabulario[] itens = await ApoioDosVocabularios.LerAsync<ItemDoVocabulario>(
            client, "regimes-funcionamento", "codigo-regime-de-funcionamento");

        itens.Should().Equal(
            new ItemDoVocabulario("INTENSIVO", "Intensivo"),
            new ItemDoVocabulario("EXTENSIVO", "Extensivo"),
            new ItemDoVocabulario("ALTERNANCIA_PEDAGOGICA", "Alternância pedagógica"));
    }

    [Fact(DisplayName = "GET /vocabularios/regimes-turno devolve os dois regimes, na ordem de declaração, sem exigir autenticação")]
    public async Task ListarRegimesDeTurno_DevolveOsDoisRegimes()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();

        ItemDoVocabulario[] itens = await ApoioDosVocabularios.LerAsync<ItemDoVocabulario>(
            client, "regimes-turno", "codigo-regime-de-turno");

        itens.Should().Equal(
            new ItemDoVocabulario("REGULAR", "Regular"),
            new ItemDoVocabulario("INTEGRAL", "Integral"));
    }

    [Fact(DisplayName = "GET /vocabularios/turnos-oferta devolve os três turnos, na ordem do dia, sem exigir autenticação")]
    public async Task ListarTurnosOferta_DevolveOsTresTurnos()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();

        ItemDoVocabulario[] itens = await ApoioDosVocabularios.LerAsync<ItemDoVocabulario>(
            client, "turnos-oferta", "codigo-turno-oferta");

        itens.Should().Equal(
            new ItemDoVocabulario("MATUTINO", "Matutino"),
            new ItemDoVocabulario("VESPERTINO", "Vespertino"),
            new ItemDoVocabulario("NOTURNO", "Noturno"));
    }

    [Theory(DisplayName = "Versão inexistente da vendor MIME do vocabulário da oferta recusa com 406")]
    [InlineData("/api/configuracao/vocabularios/programas-oferta", "codigo-programa-de-oferta")]
    [InlineData("/api/configuracao/vocabularios/formatos-pedagogicos", "codigo-formato-pedagogico")]
    [InlineData("/api/configuracao/vocabularios/regimes-funcionamento", "codigo-regime-de-funcionamento")]
    [InlineData("/api/configuracao/vocabularios/regimes-turno", "codigo-regime-de-turno")]
    [InlineData("/api/configuracao/vocabularios/turnos-oferta", "codigo-turno-oferta")]
    public async Task Vocabularios_VersaoDesconhecidaDaVendorMime_Recusa(string rota, string recurso)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri(rota, UriKind.Relative));
        request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse($"application/vnd.uniplus.{recurso}.v9+json"));

        using HttpResponseMessage resposta = await client.SendAsync(request);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotAcceptable);
    }
}
