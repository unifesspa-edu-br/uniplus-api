namespace Unifesspa.UniPlus.Portal.IntegrationTests;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Portal.IntegrationTests.Infrastructure;

/// <summary>O Portal tem pipeline próprio: o caminho que nenhuma operação atende responde 404 com o código comum da fronteira, e o método que a rota não aceita responde 405 com o código próprio.</summary>
public sealed class CaminhoInexistenteNoPortalTests(PortalApiFactory factory) : IClassFixture<PortalApiFactory>
{
    [Fact(DisplayName = "O caminho sem operação responde 404 com o código comum, sem repetir o caminho")]
    public async Task CaminhoSemOperacao_Responde404ComOCodigoComum()
    {
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage resposta = await client.GetAsync(new Uri("/api/portal/rota-que-nao-existe-x7q", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        string texto = await resposta.Content.ReadAsStringAsync();
        using JsonDocument corpo = JsonDocument.Parse(texto);
        corpo.RootElement.GetProperty("code").GetString().Should().Be("uniplus.requisicao.caminho_inexistente");
        texto.Should().NotContain("x7q");
    }

    [Fact(DisplayName = "O método que a rota não aceita responde 405 com Allow e o código do método não aceito, sem repetir o caminho")]
    public async Task MetodoNaoAceito_Responde405ComAllowEOCodigo()
    {
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage resposta = await client.PostAsync(new Uri("/api/portal/ping", UriKind.Relative), content: null);

        resposta.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        resposta.Content.Headers.Allow.Should().Contain("GET");
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        string texto = await resposta.Content.ReadAsStringAsync();
        using JsonDocument corpo = JsonDocument.Parse(texto);
        corpo.RootElement.GetProperty("code").GetString().Should().Be("uniplus.requisicao.metodo_nao_aceito");
        texto.Should().NotContain("ping");
    }
}
