namespace Unifesspa.UniPlus.Ingresso.IntegrationTests;

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using FluentAssertions;

using Unifesspa.UniPlus.IntegrationTests.Shared.Authentication;
using Unifesspa.UniPlus.Ingresso.IntegrationTests.Infrastructure;

public sealed class AuthEndpointsTests : IClassFixture<IngressoApiFactory>
{
    private static readonly Uri GetMeUri = new("/api/auth/me", UriKind.Relative);
    private readonly IngressoApiFactory _factory;

    public AuthEndpointsTests(IngressoApiFactory factory) => _factory = factory;

    [Fact]
    public async Task GetMe_ShouldReturnUnauthorized_WhenRequestDoesNotHaveToken()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(GetMeUri);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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
        payload.RootElement.GetProperty("timestamp").GetDateTime().Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }
}
