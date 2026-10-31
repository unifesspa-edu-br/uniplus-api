namespace Unifesspa.UniPlus.Infrastructure.Core.IntegrationTests.Authorization;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.IntegrationTests.Middleware;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// No pipeline real do host, endpoint sem atributo de autorização exige pessoa autenticada, e as rotas de saúde e o
/// documento OpenAPI, que são públicos, continuam respondendo sem token.
/// </summary>
public sealed class AutenticacaoPorPadraoTests : IClassFixture<InfraCoreApiFactory>
{
    private static readonly Uri SemAtributo = new("/api/teste/sem-atributo", UriKind.Relative);

    private readonly InfraCoreApiFactory _factory;

    public AutenticacaoPorPadraoTests(InfraCoreApiFactory factory) => _factory = factory;

    [Fact(DisplayName = "Endpoint sem atributo de autorização responde 401 sem token e 200 com token")]
    public async Task EndpointSemAtributo_ExigeAutenticacao()
    {
        using WebApplicationFactory<Unifesspa.UniPlus.Host.HostAssemblyMarker> comControllerDeTeste = _factory.WithWebHostBuilder(
            static builder => builder.ConfigureTestServices(static services => services
                .AddControllers()
                .AddApplicationPart(typeof(ControllerSemAtributoDeAutorizacao).Assembly)));
        using HttpClient client = comControllerDeTeste.CreateClient();

        HttpResponseMessage anonimo = await client.GetAsync(SemAtributo);
        anonimo.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using HttpRequestMessage autenticado = new(HttpMethod.Get, SemAtributo);
        autenticado.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        HttpResponseMessage resposta = await client.SendAsync(autenticado);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // As sondas que agregam verificações respondem 503 aqui, porque a fábrica não sobe o provedor de identidade: é a
    // resposta da sonda, e não a recusa por falta de token. A liveness não avalia verificação nenhuma e responde 200.
    [Theory(DisplayName = "Rotas públicas do host respondem sem token")]
    [InlineData("/health/live", HttpStatusCode.OK)]
    [InlineData("/health/ready", HttpStatusCode.ServiceUnavailable)]
    [InlineData("/health", HttpStatusCode.ServiceUnavailable)]
    [InlineData("/openapi/selecao.json", HttpStatusCode.OK)]
    public async Task RotaPublica_RespondeSemToken(string rota, HttpStatusCode esperado)
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage resposta = await client.GetAsync(new Uri(rota, UriKind.Relative));

        resposta.StatusCode.Should().Be(esperado);
    }

    [Fact(DisplayName = "Rota inexistente responde 404 sem token, sem pedir login")]
    public async Task RotaInexistente_Responde404SemToken()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage resposta = await client.GetAsync(new Uri("/api/selecao/rota-que-nao-existe", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "Método que a rota pública não aceita responde 405 sem token, sem pedir login")]
    public async Task MetodoNaoAceito_Responde405SemToken()
    {
        using HttpClient client = _factory.CreateClient();
        using StringContent corpo = new("{}");

        HttpResponseMessage resposta = await client.PostAsync(new Uri("/api/configuracao/vocabularios/tipos-banca", UriKind.Relative), corpo);

        resposta.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }
}

/// <summary>Controller sem nenhum atributo de autorização, que só existe neste teste.</summary>
[ApiController]
[Route("sem-atributo")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public.")]
public sealed class ControllerSemAtributoDeAutorizacao : ControllerBase
{
    [HttpGet]
    public IActionResult Ler() => Ok();
}
