namespace Unifesspa.UniPlus.Selecao.IntegrationTests;

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Infrastructure;

public sealed class AuthEndpointsTests : IClassFixture<SelecaoApiFactory>
{
    private static readonly Uri GetMeUri = new("/api/auth/me", UriKind.Relative);
    private readonly SelecaoApiFactory _factory;

    public AuthEndpointsTests(SelecaoApiFactory factory) => _factory = factory;

    [Fact]
    public async Task GetMe_ShouldReturnProblemDetails_WhenRequestDoesNotHaveToken()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(GetMeUri);

        await AssertUnauthorizedProblemDetails(response);
    }

    [Fact]
    public async Task GetMe_ShouldReturnProblemDetails_WhenTokenIsInvalid()
    {
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme,
            "bogus-token");

        HttpResponseMessage response = await client.GetAsync(GetMeUri);

        await AssertUnauthorizedProblemDetails(response);
    }

    private static async Task AssertUnauthorizedProblemDetails(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        response.Headers.WwwAuthenticate.Should().NotBeEmpty();
        response.Headers.WwwAuthenticate.First().Scheme.Should().Be("Bearer");

        using JsonDocument payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        JsonElement root = payload.RootElement;

        root.GetProperty("status").GetInt32().Should().Be(401);
        root.GetProperty("type").GetString()
            .Should().Be("https://uniplus.unifesspa.edu.br/errors/uniplus.auth.unauthorized");
        root.GetProperty("title").GetString().Should().Be("Não autenticado");
        root.GetProperty("code").GetString().Should().Be("uniplus.auth.unauthorized");
        root.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
        root.GetProperty("instance").GetString().Should().StartWith("urn:uuid:");
    }

    [Fact]
    public async Task GetMe_ShouldReturnAuthenticatedUser_WhenRequestHasMockJwt()
    {
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme,
            TestAuthHandler.TokenValue);
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, "4dc27b1b-c56f-488a-a7a2-2a30bdbc08b8");
        client.DefaultRequestHeaders.Add(TestAuthHandler.NameHeader, "Usuario Admin");
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, "admin@teste.unifesspa.edu.br");
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "admin,gestor");

        HttpResponseMessage response = await client.GetAsync(GetMeUri);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        payload.RootElement.GetProperty("userId").GetString().Should().Be("4dc27b1b-c56f-488a-a7a2-2a30bdbc08b8");
        payload.RootElement.GetProperty("name").GetString().Should().Be("Usuario Admin");
        payload.RootElement.GetProperty("email").GetString().Should().Be("admin@teste.unifesspa.edu.br");
        payload.RootElement.GetProperty("roles").EnumerateArray().Select(static role => role.GetString())
            .Should().BeEquivalentTo(["admin", "gestor"]);
        payload.RootElement.GetProperty("timestamp").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }
}
