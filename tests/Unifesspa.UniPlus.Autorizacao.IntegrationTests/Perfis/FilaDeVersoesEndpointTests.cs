namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Perfis;

using System.Net;
using System.Net.Http.Headers;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

[Collection(AutorizacaoEndpointCollection.Name)]
public sealed class FilaDeVersoesEndpointTests(AutorizacaoEndpointFixture fixture) : PerfilEndpointTestBase(fixture)
{
    private const string RotaDaFila = "/api/autorizacao/versoes-de-perfil?fila=GESTAO_DE_ACESSOS";
    private const string Tipo = "application/vnd.uniplus.fila-de-versoes.v1+json";

    [Fact(DisplayName = "A fila devolve o tipo de mídia da versão solicitada")]
    public async Task Fila_VersaoExistente_DevolveTipoVersionado()
    {
        using HttpClient gestor = await ClienteDoGestorAsync();
        using HttpResponseMessage resposta = await ConsultarAsync(gestor, Tipo);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        resposta.Content.Headers.ContentType!.MediaType.Should().Be(Tipo);
    }

    [Fact(DisplayName = "A fila devolve a versão atual para application/json")]
    public async Task Fila_JsonSemVersao_DevolveTipoDaVersaoAtual()
    {
        using HttpClient gestor = await ClienteDoGestorAsync();
        using HttpResponseMessage resposta = await ConsultarAsync(gestor, "application/json");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        resposta.Content.Headers.ContentType!.MediaType.Should().Be(Tipo);
    }

    [Fact(DisplayName = "A fila informa as versões disponíveis quando a versão pedida não existe")]
    public async Task Fila_VersaoInexistente_Devolve406()
    {
        using HttpClient gestor = await ClienteDoGestorAsync();
        using HttpResponseMessage resposta = await ConsultarAsync(gestor, "application/vnd.uniplus.fila-de-versoes.v99+json");

        await VerificarRecusaDeVersaoAsync(resposta);
    }

    [Theory(DisplayName = "A fila exige autenticação e permissão")]
    [InlineData(false, HttpStatusCode.Unauthorized)]
    [InlineData(true, HttpStatusCode.Forbidden)]
    public async Task Fila_SemAcesso_Recusa(bool autenticado, HttpStatusCode esperado)
    {
        using HttpClient cliente = autenticado ? Cliente("pessoa.sem.permissao") : Fixture.Factory.CreateClient();
        using HttpResponseMessage resposta = await ConsultarAsync(cliente, Tipo);

        resposta.StatusCode.Should().Be(esperado);
    }

    private static async Task<HttpResponseMessage> ConsultarAsync(HttpClient cliente, string tipo)
    {
        using HttpRequestMessage pedido = new(HttpMethod.Get, new Uri(RotaDaFila, UriKind.Relative));
        pedido.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(tipo));
        return await cliente.SendAsync(pedido);
    }
}
