namespace Unifesspa.UniPlus.Selecao.IntegrationTests;

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using FluentAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Infrastructure;

public sealed class ProfileEndpointsTests : IClassFixture<SelecaoApiFactory>
{
    private static readonly Uri GetMeUri = new("/api/profile/me", UriKind.Relative);
    private readonly SelecaoApiFactory _factory;

    public ProfileEndpointsTests(SelecaoApiFactory factory) => _factory = factory;

    [Fact]
    public async Task GetMe_ShouldReturnUnauthorized_WhenRequestDoesNotHaveToken()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(GetMeUri);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMe_ShouldReturnProfile_WithCpfAndNomeSocial_WhenTokenIncludesUniPlusScope()
    {
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme,
            TestAuthHandler.TokenValue);
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, "4dc27b1b-c56f-488a-a7a2-2a30bdbc08b8");
        client.DefaultRequestHeaders.Add(TestAuthHandler.NameHeader, "Maria Santos");
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, "maria@teste.unifesspa.edu.br");
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "candidato");
        client.DefaultRequestHeaders.Add(TestAuthHandler.CpfHeader, "529.982.247-25");
        client.DefaultRequestHeaders.Add(TestAuthHandler.NomeSocialHeader, "Maria dos Santos");

        HttpResponseMessage response = await client.GetAsync(GetMeUri);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        payload.RootElement.GetProperty("userId").GetString().Should().Be("4dc27b1b-c56f-488a-a7a2-2a30bdbc08b8");
        payload.RootElement.GetProperty("name").GetString().Should().Be("Maria Santos");
        payload.RootElement.GetProperty("email").GetString().Should().Be("maria@teste.unifesspa.edu.br");
        payload.RootElement.GetProperty("cpf").GetString().Should().Be("529.982.247-25");
        payload.RootElement.GetProperty("nomeSocial").GetString().Should().Be("Maria dos Santos");
        payload.RootElement.GetProperty("roles").EnumerateArray().Select(static role => role.GetString())
            .Should().BeEquivalentTo(["candidato"]);
        payload.RootElement.GetProperty("timestamp").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task GetMe_ShouldReturnNullCpfAndNomeSocial_WhenUniPlusClaimsAbsent()
    {
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme,
            TestAuthHandler.TokenValue);

        HttpResponseMessage response = await client.GetAsync(GetMeUri);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        payload.RootElement.GetProperty("cpf").ValueKind.Should().Be(JsonValueKind.Null);
        payload.RootElement.GetProperty("nomeSocial").ValueKind.Should().Be(JsonValueKind.Null);
    }
}
