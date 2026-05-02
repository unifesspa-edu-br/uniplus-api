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

    /// <summary>
    /// <see cref="AuthOptions.ClockSkew"/> default consumido pelo <c>JwtBearer</c> em produção (30s).
    /// O cenário de expiração precisa esperar além dessa janela para que o pipeline real reconheça
    /// o token como expirado.
    /// </summary>
    private static readonly TimeSpan ExpectedClockSkew = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Margem adicional para absorver drift de relógio entre o host onde o teste roda e o container
    /// do Keycloak (timestamps do <c>exp</c> claim são gerados pelo container, mas a comparação no
    /// pipeline acontece no host). 5 segundos cobrem cenários reais de CI sem inflar a duração da
    /// suíte.
    /// </summary>
    private static readonly TimeSpan ClockDriftBuffer = TimeSpan.FromSeconds(5);

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
        KeycloakTestUser user = KeycloakTestUsers.Admin;
        string accessToken = await _keycloak.RequestAccessTokenAsync(user.Username, KeycloakTestUsers.SharedPassword);
        using HttpClient client = CreateClientWithToken(accessToken);

        using HttpResponseMessage response = await client.GetAsync(AuthMeUri);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        payload.RootElement.GetProperty("userId").GetString().Should().NotBeNullOrWhiteSpace();
        payload.RootElement.GetProperty("email").GetString().Should().Be(user.Email);
        payload.RootElement.GetProperty("roles").EnumerateArray().Select(static r => r.GetString())
            .Should().Contain(user.Role);
    }

    [Fact]
    public async Task GetProfileMe_ShouldReturnCpfAndNomeSocial_WhenTokenCarriesUniPlusProfileScope()
    {
        KeycloakTestUser user = KeycloakTestUsers.Candidato;
        string accessToken = await _keycloak.RequestAccessTokenAsync(user.Username, KeycloakTestUsers.SharedPassword);
        using HttpClient client = CreateClientWithToken(accessToken);

        using HttpResponseMessage response = await client.GetAsync(ProfileMeUri);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        payload.RootElement.GetProperty("cpf").GetString().Should().Be(user.Cpf);
        payload.RootElement.GetProperty("nomeSocial").GetString().Should().Be(user.NomeSocial);
        payload.RootElement.GetProperty("email").GetString().Should().Be(user.Email);
        payload.RootElement.GetProperty("roles").EnumerateArray().Select(static r => r.GetString())
            .Should().Contain(user.Role);
    }

    [Fact]
    public async Task GetAuthMe_ShouldReturnUnauthorized_WhenTokenIsExpired()
    {
        // Solicita um token com vida curta (access.token.lifespan=5s no realm) e aguarda até passar
        // do exp do PRÓPRIO token + ClockSkew configurado + buffer de drift. Calcular a partir do exp
        // emitido pelo container — em vez de um delay fixo — torna o teste resiliente a (a) drift de
        // relógio entre host e container e (b) eventuais bumps futuros do lifespan ou do ClockSkew
        // sem precisar atualizar este caso.
        string accessToken = await _keycloak.RequestAccessTokenAsync(
            KeycloakTestUsers.Admin.Username,
            KeycloakTestUsers.SharedPassword);

        DateTimeOffset waitUntil = ReadExpirationFromToken(accessToken) + ExpectedClockSkew + ClockDriftBuffer;
        TimeSpan remaining = waitUntil - DateTimeOffset.UtcNow;
        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining);
        }

        using HttpClient client = CreateClientWithToken(accessToken);

        using HttpResponseMessage response = await client.GetAsync(AuthMeUri);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAuthMe_ShouldReturnUnauthorized_WhenTokenAudienceDoesNotIncludeUniPlus()
    {
        // O client e2e-tests-bad-aud não inclui o scope uniplus-profile (que carrega o audience mapper),
        // portanto o token emitido não traz aud=uniplus.
        string accessToken = await _keycloak.RequestAccessTokenAsync(
            KeycloakTestUsers.Admin.Username,
            KeycloakTestUsers.SharedPassword,
            KeycloakContainerFixture.BadAudienceClientId);
        using HttpClient client = CreateClientWithToken(accessToken);

        using HttpResponseMessage response = await client.GetAsync(AuthMeUri);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAuthMe_ShouldReturnUnauthorized_WhenTokenIsSignedByExternalKey()
    {
        string forgedToken = ForgeTokenSignedByExternalKey(_keycloak.Authority);
        using HttpClient client = CreateClientWithToken(forgedToken);

        using HttpResponseMessage response = await client.GetAsync(AuthMeUri);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAuthMe_ShouldReturnUnauthorized_WhenTokenIssuerDoesNotMatchAuthority()
    {
        // Isola estritamente ValidateIssuer: o token é assinado pela chave conhecida que o realm
        // sintético publica no JWKS (KeycloakKnownTestKey, embutida via components.KeyProvider em
        // realm-e2e-tests.json), portanto ValidateIssuerSigningKey passa. Audience e lifetime
        // também são corretos. A ÚNICA dimensão inválida é o iss claim — uma regressão futura que
        // desligue ValidateIssuer (ex.: ValidateIssuer = false) faria este caso passar a aceitar
        // o token, capturando a regressão. Complementa o cenário 5 (que isola signing key).
        string forgedToken = ForgeTokenWithKnownKey(
            issuer: "https://impostor.example.com/realms/wrong",
            audience: KeycloakContainerFixture.Audience);
        using HttpClient client = CreateClientWithToken(forgedToken);

        using HttpResponseMessage response = await client.GetAsync(AuthMeUri);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetHealth_ShouldReportHealthy_WhenKeycloakDiscoveryEndpointResponds()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(HealthUri);

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

    private static DateTimeOffset ReadExpirationFromToken(string accessToken)
    {
        JsonWebToken jwt = new JsonWebTokenHandler().ReadJsonWebToken(accessToken);
        return new DateTimeOffset(jwt.ValidTo, TimeSpan.Zero);
    }

    private static string ForgeTokenSignedByExternalKey(string issuer)
    {
        using RSA rsa = RSA.Create(2048);
        RsaSecurityKey signingKey = new(rsa.ExportParameters(includePrivateParameters: true))
        {
            KeyId = "external-test-key",
        };

        return ForgeToken(
            signingKey,
            issuer,
            audience: KeycloakContainerFixture.Audience);
    }

    private static string ForgeTokenWithKnownKey(string issuer, string audience)
    {
        RsaSecurityKey signingKey = KeycloakKnownTestKey.CreateSigningKey();
        return ForgeToken(signingKey, issuer, audience);
    }

    private static string ForgeToken(RsaSecurityKey signingKey, string issuer, string audience)
    {
        SigningCredentials credentials = new(signingKey, SecurityAlgorithms.RsaSha256);
        DateTime now = DateTime.UtcNow;
        SecurityTokenDescriptor descriptor = new()
        {
            Issuer = issuer,
            Audience = audience,
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
