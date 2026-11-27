namespace Unifesspa.UniPlus.Host.IntegrationTests;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// O método que o caminho não aceita responde 405 com o cabeçalho Allow preservado e o corpo de erro com o código próprio,
/// sem repetir o caminho nem o método pedidos (ADR-0023).
/// </summary>
[Collection(MonolitoHostCollection.Name)]
public sealed class MetodoNaoAceitoTests(MonolitoPostgresFixture fixture)
{
    private const string Codigo = "uniplus.requisicao.metodo_nao_aceito";
    private const string OrigemDoSpa = "http://localhost:4200";

    [Fact(DisplayName = "O método é conferido antes do formato do identificador: 405 com Allow e o código do método não aceito")]
    public async Task MetodoQueOCaminhoNaoAceita_E405AntesDoFormatoDoIdentificador()
    {
        using HttpClient client = fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await client.GetAsync(new Uri("/api/configuracao/admin/campi/abc-x7q", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        resposta.Content.Headers.Allow.Should().Contain("DELETE");
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        string texto = await resposta.Content.ReadAsStringAsync();
        using JsonDocument corpo = JsonDocument.Parse(texto);
        corpo.RootElement.GetProperty("code").GetString().Should().Be(Codigo);
        corpo.RootElement.GetProperty("type").GetString().Should().EndWith($"/erros/{Codigo}");
        corpo.RootElement.GetProperty("status").GetInt32().Should().Be(405);
        texto.Should().NotContain("x7q");
    }

    [Fact(DisplayName = "O método que o caminho não aceita responde 405 com Allow e o código do método não aceito")]
    public async Task MetodoNaoAceito_Responde405ComAllowEOCodigo()
    {
        using HttpClient client = fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await client.PostAsync(new Uri("/api/configuracao/campi", UriKind.Relative), content: null);

        resposta.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        resposta.Content.Headers.Allow.Should().Contain("GET");
        await AfirmarCodigoAsync(resposta);
    }

    [Fact(DisplayName = "O HEAD num caminho que só aceita leitura por GET responde 405 com Allow e tipo do conteúdo")]
    public async Task Head_NumCaminhoDeGet_Responde405ComAllow()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        using HttpRequestMessage pedido = new(HttpMethod.Head, new Uri("/api/configuracao/campi", UriKind.Relative));

        HttpResponseMessage resposta = await client.SendAsync(pedido);

        resposta.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        resposta.Content.Headers.Allow.Should().Contain("GET");
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact(DisplayName = "O preflight de origem permitida segue o CORS: 204 com a origem permitida e sem o código do método não aceito")]
    public async Task Preflight_DeOrigemPermitida_Responde204SemOCodigo()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        using HttpRequestMessage pedido = new(HttpMethod.Options, new Uri("/api/configuracao/campi", UriKind.Relative));
        pedido.Headers.Add("Origin", OrigemDoSpa);
        pedido.Headers.Add("Access-Control-Request-Method", "GET");

        HttpResponseMessage resposta = await client.SendAsync(pedido);

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        resposta.Headers.GetValues("Access-Control-Allow-Origin").Should().Contain(OrigemDoSpa);
        (await resposta.Content.ReadAsStringAsync()).Should().NotContain(Codigo);
    }

    [Fact(DisplayName = "O OPTIONS sem preflight num caminho que não o aceita responde 405 com o código do método não aceito")]
    public async Task Options_SemPreflight_Responde405ComOCodigo()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        using HttpRequestMessage pedido = new(HttpMethod.Options, new Uri("/api/configuracao/campi", UriKind.Relative));

        HttpResponseMessage resposta = await client.SendAsync(pedido);

        resposta.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        await AfirmarCodigoAsync(resposta);
    }

    [Fact(DisplayName = "O 401 de rota protegida com identificador válido e sem login continua com o código de não autenticado")]
    public async Task RotaProtegida_SemLogin_Continua401()
    {
        using HttpClient client = fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await client.DeleteAsync(new Uri($"/api/configuracao/admin/campi/{Guid.CreateVersion7()}", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using JsonDocument corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        corpo.RootElement.GetProperty("code").GetString().Should().Be("uniplus.auth.unauthorized");
    }

    private static async Task AfirmarCodigoAsync(HttpResponseMessage resposta)
    {
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        using JsonDocument corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        corpo.RootElement.GetProperty("code").GetString().Should().Be(Codigo);
    }
}
