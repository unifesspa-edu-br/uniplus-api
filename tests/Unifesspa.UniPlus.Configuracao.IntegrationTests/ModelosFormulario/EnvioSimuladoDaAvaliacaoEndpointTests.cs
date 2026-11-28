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
        paginas = new[] { new { codigo = "DADOS", exibicao = (object?)null, itens = Array.Empty<object>(), grupos = Array.Empty<object>() } },
        termos = Array.Empty<object>(),
        derivacoes = Array.Empty<object>(),
        agregados = Array.Empty<object>(),
    };

    private readonly ConfiguracaoEndpointFixture _fixture;

    public EnvioSimuladoDaAvaliacaoEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory(DisplayName = "Envio simulado com o instante omitido ou nulo é recusado com 422 e o código genérico de obrigatório")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Envio_InstanteOmitidoOuNulo_Retorna422(bool nulo)
    {
        object envio = nulo ? new { instante = (string?)null, fuso = "America/Belem" } : new { fuso = "America/Belem" };

        using HttpResponseMessage resposta = await AvaliarAsync(new { regras = RegrasSemItens, envio });

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        JsonElement erro = corpo.RootElement.GetProperty("errors").EnumerateArray().Should().ContainSingle().Subject;
        (erro.GetProperty("field").GetString(), erro.GetProperty("code").GetString()).Should().Be(("envio.instante", "uniplus.validacao.obrigatorio"));
    }

    [Fact(DisplayName = "Envio simulado sem instante e com zona desconhecida devolve as duas recusas juntas")]
    public async Task Envio_SemInstanteEComFusoDesconhecido_AcumulaAsRecusas()
    {
        using HttpResponseMessage resposta = await AvaliarAsync(new { regras = RegrasSemItens, envio = new { fuso = "America/Atlantida" } });

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        corpo.RootElement.GetProperty("errors").EnumerateArray().Select(static e => e.GetProperty("field").GetString())
            .Should().Equal("envio.instante", "envio.fuso");
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
