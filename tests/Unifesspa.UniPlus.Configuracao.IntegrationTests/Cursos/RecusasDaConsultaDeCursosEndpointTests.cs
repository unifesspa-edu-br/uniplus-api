namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Cursos;

using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class RecusasDaConsultaDeCursosEndpointTests(ConfiguracaoEndpointFixture fixture)
{
    [Fact]
    public async Task Listar_OrdenacaoMalformadaEBuscaLonga_RecusaOrdenacao()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        using HttpResponseMessage resposta = await client.GetAsync(
            new Uri($"/api/configuracao/cursos?sort=nome%2C&q={new string('a', 201)}", UriKind.Relative));
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        string corpo = await resposta.Content.ReadAsStringAsync();
        corpo.Should().Contain("uniplus.consulta.ordenacao_mal_formada");
    }
}
