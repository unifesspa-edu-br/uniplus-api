namespace Unifesspa.UniPlus.Host.IntegrationTests;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// O caminho que nenhuma operação atende (rota que não existe, identificador de rota fora do formato) responde 404 com o
/// corpo de erro e o código comum da fronteira; o 404 de registro inexistente continua com o código do assunto (ADR-0023).
/// </summary>
[Collection(MonolitoHostCollection.Name)]
public sealed class CaminhoInexistenteTests(MonolitoPostgresFixture fixture)
{
    private const string Codigo = "uniplus.requisicao.caminho_inexistente";

    [Theory(DisplayName = "O caminho sem operação, com ou sem identificador fora do formato, responde 404 com o código comum e sem repetir o caminho")]
    [InlineData("GET", "/api/rota-que-nenhum-modulo-declara-x7q")]
    [InlineData("GET", "/api/configuracao/campi/abc-x7q")]
    [InlineData("DELETE", "/api/configuracao/admin/campi/abc-x7q")]
    [InlineData("GET", "/health/xyz-x7q")]
    [InlineData("GET", "/openapi/inexistente-x7q.json")]
    [InlineData("GET", "/openapi/x7q%3Cb%3E.json")]
    public async Task CaminhoSemOperacao_Responde404ComOCodigoComum(string metodo, string caminho)
    {
        HttpResponseMessage resposta = await EnviarAsync(metodo, caminho);

        await ConferirCaminhoInexistenteAsync(resposta);
    }

    [Fact(DisplayName = "O documento de contrato com nome longo e codificado responde 404 com o código comum, sem repetir o nome")]
    public async Task DocumentoDeContratoComNomeLongoECodificado_Responde404ComOCodigoComum()
    {
        HttpResponseMessage resposta = await EnviarAsync("GET", $"/openapi/x7q{new string('a', 500)}%20%3Cb%3E.json");

        await ConferirCaminhoInexistenteAsync(resposta);
    }

    [Fact(DisplayName = "O documento de contrato só vale com o nome publicado, em minúsculas: outra grafia responde 404 comum")]
    public async Task DocumentoDeContratoComOutraGrafia_Responde404ComOCodigoComum()
    {
        HttpResponseMessage resposta = await EnviarAsync("GET", "/openapi/SELECAO.json");

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using JsonDocument corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        corpo.RootElement.GetProperty("code").GetString().Should().Be(Codigo);
    }

    [Fact(DisplayName = "O 404 de registro inexistente mantém o código do assunto e não recebe o código comum")]
    public async Task RegistroInexistente_MantemOCodigoDoAssunto()
    {
        using HttpClient client = fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await client.GetAsync(new Uri($"/api/publicacoes/tipos-ato/{Guid.CreateVersion7()}", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using JsonDocument corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        corpo.RootElement.GetProperty("code").GetString().Should().NotBe(Codigo).And.EndWith(".nao_encontrado");
    }

    [Theory(DisplayName = "As rotas que existem não mudam: saúde e documento de contrato respondem 200")]
    [InlineData("/health/live")]
    [InlineData("/openapi/selecao.json")]
    public async Task RotaQueExiste_NaoMuda(string caminho)
    {
        using HttpClient client = fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await client.GetAsync(new Uri(caminho, UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<HttpResponseMessage> EnviarAsync(string metodo, string caminho)
    {
        using HttpClient client = fixture.Factory.CreateClient();
        using HttpRequestMessage pedido = new(new HttpMethod(metodo), new Uri(caminho, UriKind.Relative));

        return await client.SendAsync(pedido);
    }

    private static async Task ConferirCaminhoInexistenteAsync(HttpResponseMessage resposta)
    {
        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        string texto = await resposta.Content.ReadAsStringAsync();
        using JsonDocument corpo = JsonDocument.Parse(texto);
        corpo.RootElement.GetProperty("code").GetString().Should().Be(Codigo);
        corpo.RootElement.GetProperty("type").GetString().Should().EndWith($"/erros/{Codigo}");
        corpo.RootElement.GetProperty("status").GetInt32().Should().Be(404);
        corpo.RootElement.GetProperty("traceId").GetString().Should().HaveLength(32);
        texto.Should().NotContain("x7q");
    }
}
