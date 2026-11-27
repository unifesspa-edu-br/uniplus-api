namespace Unifesspa.UniPlus.Portal.IntegrationTests;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Portal.IntegrationTests.Infrastructure;

/// <summary>O Portal tem pipeline próprio: o caminho que nenhuma operação atende responde 404 com o código comum da fronteira.</summary>
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
}
