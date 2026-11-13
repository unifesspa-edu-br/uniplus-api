namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// O envio que a simulação supõe traz o instante: sem ele a avaliação é recusada, e o instante nunca é preenchido
/// com o relógio do servidor.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class EnvioSimuladoDaAvaliacaoEndpointTests
{
    private const string Rota = "/api/configuracao/admin/avaliacoes-de-formulario";

    private static readonly object RegrasSemItens = new
    {
        etapas = new[] { new { codigo = "DADOS", exibicao = (object?)null, itens = Array.Empty<object>(), grupos = Array.Empty<object>() } },
        termos = Array.Empty<object>(),
        derivacoes = Array.Empty<object>(),
        agregados = Array.Empty<object>(),
    };

    private readonly ConfiguracaoEndpointFixture _fixture;

    public EnvioSimuladoDaAvaliacaoEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Envio simulado sem instante é recusado com 400 e nomeia o campo ausente")]
    public async Task Envio_SemInstante_Retorna400()
    {
        using HttpResponseMessage resposta = await AvaliarAsync(new { regras = RegrasSemItens, envio = new { fuso = "America/Belem" } });

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using JsonDocument corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        corpo.RootElement.GetProperty("code").GetString().Should().Be("uniplus.requisicao.campo_obrigatorio_ausente");
        corpo.RootElement.GetProperty("detail").GetString().Should().Contain("instante");
    }

    [Fact(DisplayName = "Envio simulado com instante, mesmo com 7 casas decimais, é avaliado")]
    public async Task Envio_ComInstante_EAvaliado()
    {
        using HttpResponseMessage resposta = await AvaliarAsync(
            new { regras = RegrasSemItens, envio = new { instante = "2026-11-12T10:00:00.9999999Z", fuso = "America/Belem" } });

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
    }

    private async Task<HttpResponseMessage> AvaliarAsync(object corpo)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage requisicao = new(HttpMethod.Post, new Uri(Rota, UriKind.Relative)) { Content = JsonContent.Create(corpo) };
        requisicao.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        requisicao.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");

        return await client.SendAsync(requisicao);
    }
}
