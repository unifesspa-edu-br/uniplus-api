namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Authentication;

using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;

using FluentAssertions;

using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// Cobertura ponta-a-ponta do pipeline real <c>JwtBearer</c> contra um Keycloak provisionado via
/// Testcontainers. Cada caso isola UMA falha de validação (issuer/audience/lifetime/signing key) ou
/// um caminho feliz, sem mocks no esquema de autenticação. Documentação operacional em
/// <c>docs/testing-e2e-auth.md</c>.
/// </summary>
[Collection(KeycloakContainerFixture.CollectionName)]
public sealed class AuthE2ETests : IClassFixture<OidcRealApiFactoryWrapper>
{
    private static readonly Uri AuthMeUri = new("/api/auth/me", UriKind.Relative);
    private static readonly Uri ProfileMeUri = new("/api/profile/me", UriKind.Relative);
    private static readonly Uri HealthUri = new("/health", UriKind.Relative);
    private const string AdminUsername = "admin";
    private const string CandidatoUsername = "candidato";
    private const string SharedPassword = "Changeme!123";

    private readonly KeycloakContainerFixture _keycloak;
    private readonly OidcRealApiFactory _factory;

    public AuthE2ETests(KeycloakContainerFixture keycloak, OidcRealApiFactoryWrapper factoryWrapper)
    {
        ArgumentNullException.ThrowIfNull(keycloak);
        ArgumentNullException.ThrowIfNull(factoryWrapper);
        _keycloak = keycloak;
        _factory = factoryWrapper.GetOrCreate(keycloak);
    }

    [Fact]
    public async Task GetAuthMe_ShouldReturnAuthenticatedUser_WhenTokenIsValid()
    {
        string accessToken = await _keycloak.RequestAccessTokenAsync(AdminUsername, SharedPassword);
        using HttpClient client = CreateClientWithToken(accessToken);

        HttpResponseMessage response = await client.GetAsync(AuthMeUri);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        payload.RootElement.GetProperty("userId").GetString().Should().NotBeNullOrWhiteSpace();
        payload.RootElement.GetProperty("email").GetString().Should().Be("admin@e2e.uniplus.local");
        payload.RootElement.GetProperty("roles").EnumerateArray().Select(static r => r.GetString())
            .Should().Contain("admin");
    }

    [Fact]
    public async Task GetProfileMe_ShouldReturnCpfAndNomeSocial_WhenTokenCarriesUniPlusProfileScope()
    {
        string accessToken = await _keycloak.RequestAccessTokenAsync(CandidatoUsername, SharedPassword);
        using HttpClient client = CreateClientWithToken(accessToken);

        HttpResponseMessage response = await client.GetAsync(ProfileMeUri);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        payload.RootElement.GetProperty("cpf").GetString().Should().Be("24843803480");
        payload.RootElement.GetProperty("nomeSocial").GetString().Should().Be("Candidato Teste");
        payload.RootElement.GetProperty("email").GetString().Should().Be("candidato@e2e.uniplus.local");
        payload.RootElement.GetProperty("roles").EnumerateArray().Select(static r => r.GetString())
            .Should().Contain("candidato");
    }

    [Fact]
    public async Task GetAuthMe_ShouldReturnUnauthorized_WhenTokenIsExpired()
    {
        // O client e2e-tests é configurado com access.token.lifespan=5s no realm-export.json.
        // Aguarda além do ClockSkew (30s default) + lifespan para garantir que o token efetivamente
        // expirou na visão do JwtBearer, e não apenas na visão nominal do exp claim.
        string accessToken = await _keycloak.RequestAccessTokenAsync(AdminUsername, SharedPassword);
        await Task.Delay(TimeSpan.FromSeconds(36));

        using HttpClient client = CreateClientWithToken(accessToken);

        HttpResponseMessage response = await client.GetAsync(AuthMeUri);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAuthMe_ShouldReturnUnauthorized_WhenTokenAudienceDoesNotIncludeUniPlus()
    {
        // O client e2e-tests-bad-aud não inclui o scope uniplus-profile (que carrega o audience mapper),
        // portanto o token emitido não traz aud=uniplus.
        string accessToken = await _keycloak.RequestAccessTokenAsync(
            AdminUsername,
            SharedPassword,
            KeycloakContainerFixture.BadAudienceClientId);
        using HttpClient client = CreateClientWithToken(accessToken);

        HttpResponseMessage response = await client.GetAsync(AuthMeUri);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAuthMe_ShouldReturnUnauthorized_WhenTokenIsSignedByExternalKey()
    {
        string forgedToken = ForgeTokenSignedByExternalKey(_keycloak.Authority);
        using HttpClient client = CreateClientWithToken(forgedToken);

        HttpResponseMessage response = await client.GetAsync(AuthMeUri);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetHealth_ShouldReportHealthy_WhenKeycloakDiscoveryEndpointResponds()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(HealthUri);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        string body = await response.Content.ReadAsStringAsync();
        body.Should().Be("Healthy");
    }

    private HttpClient CreateClientWithToken(string accessToken)
    {
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private static string ForgeTokenSignedByExternalKey(string issuer)
    {
        using RSA rsa = RSA.Create(2048);
        RsaSecurityKey signingKey = new(rsa.ExportParameters(includePrivateParameters: true))
        {
            KeyId = "external-test-key",
        };

        SigningCredentials credentials = new(signingKey, SecurityAlgorithms.RsaSha256);
        DateTime now = DateTime.UtcNow;
        SecurityTokenDescriptor descriptor = new()
        {
            Issuer = issuer,
            Audience = KeycloakContainerFixture.Audience,
            NotBefore = now,
            Expires = now.AddMinutes(5),
            SigningCredentials = credentials,
            Claims = new Dictionary<string, object>
            {
                ["sub"] = Guid.NewGuid().ToString(),
                ["preferred_username"] = "intruder",
                ["email"] = "intruder@example.com",
            },
        };

        JsonWebTokenHandler handler = new();
        return handler.CreateToken(descriptor);
    }
}
