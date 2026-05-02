namespace Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

/// <summary>
/// Sobe um container do Keycloak real (imagem composta canônica do projeto, com SPI <c>cpf-matcher</c>
/// embutido) importando o <c>realm-export.json</c> versionado em <c>docker/keycloak/</c>. A fixture é
/// compartilhada entre testes via <c>[Collection("Keycloak")]</c> — ver <see cref="KeycloakCollection"/>.
///
/// Garante que a suíte E2E exercite o pipeline real <c>JwtBearer</c> da API contra o IdP de produção,
/// sem mocks no esquema de autenticação.
/// </summary>
public sealed class KeycloakContainerFixture : IAsyncLifetime
{
    /// <summary>
    /// Imagem composta canônica do projeto. Patch fixo alinhado ao <c>docker/docker-compose.yml</c>
    /// para garantir parity dev/CI/prod e evitar deriva silenciosa em <c>1.x</c>.
    /// </summary>
    public const string Image = "ghcr.io/unifesspa-edu-br/uniplus-keycloak:1.0.2";

    /// <summary>
    /// Nome convencional da xUnit collection que compartilha esta fixture entre classes de teste.
    /// Cada assembly de testes precisa declarar sua própria <c>[CollectionDefinition]</c> com este nome,
    /// porque xUnit exige que a definição da collection viva no mesmo assembly que a usa.
    /// </summary>
    public const string CollectionName = "Keycloak";

    /// <summary>
    /// Nome do realm sintético dedicado APENAS aos testes E2E. Diferente do realm canônico
    /// (<c>unifesspa</c>) usado em dev/homologação/produção, este realm vive em arquivo separado
    /// (<c>docker/keycloak/realm-e2e-tests.json</c>) e jamais é montado em compose ou Helm —
    /// existe exclusivamente para o ciclo desta fixture.
    /// </summary>
    public const string RealmName = "unifesspa-e2e";
    /// <summary>Client confidencial principal — emite tokens com <c>aud=uniplus</c>.</summary>
    public const string ClientId = "e2e-tests";

    /// <summary>
    /// Client confidencial sem o scope <c>uniplus-profile</c> — emite tokens cuja audience não inclui
    /// <c>uniplus</c>. Usado para validar a rejeição por audience inválida.
    /// </summary>
    public const string BadAudienceClientId = "e2e-tests-bad-aud";

    public const string ClientSecret = "e2e-secret";
    public const string Audience = "uniplus";

    private const ushort KeycloakHttpPort = 8080;
    private const string RealmExportContainerPath = "/opt/keycloak/data/import/realm-export.json";

    private readonly IContainer _container;
    private HttpClient? _httpClient;

    public KeycloakContainerFixture()
    {
        string realmExportHostPath = ResolveRealmExportHostPath();

        _container = new ContainerBuilder(Image)
            .WithPortBinding(KeycloakHttpPort, true)
            .WithEnvironment("KC_BOOTSTRAP_ADMIN_USERNAME", "admin")
            .WithEnvironment("KC_BOOTSTRAP_ADMIN_PASSWORD", "admin")
            .WithEnvironment("KC_HEALTH_ENABLED", "true")
            .WithEnvironment("KC_HOSTNAME_STRICT", "false")
            .WithEnvironment("KC_HTTP_ENABLED", "true")
            .WithBindMount(realmExportHostPath, RealmExportContainerPath)
            .WithCommand("start-dev", "--import-realm")
            .WithWaitStrategy(
                Wait.ForUnixContainer()
                    .UntilHttpRequestIsSucceeded(request => request
                        .ForPort(KeycloakHttpPort)
                        .ForPath($"/realms/{RealmName}/.well-known/openid-configuration")
                        .ForStatusCode(HttpStatusCode.OK)))
            .Build();
    }

    /// <summary>URL base do Keycloak no host (inclui porta efêmera publicada).</summary>
    public Uri BaseAddress => new($"http://{_container.Hostname}:{_container.GetMappedPublicPort(KeycloakHttpPort)}");

    /// <summary>Authority OIDC do realm — valor a ser injetado em <c>Auth:Authority</c>.</summary>
    public string Authority => new Uri(BaseAddress, $"/realms/{RealmName}").ToString();

    /// <summary>Endpoint de token (OIDC token endpoint do realm).</summary>
    public Uri TokenEndpoint => new(BaseAddress, $"/realms/{RealmName}/protocol/openid-connect/token");

    /// <summary>Endpoint de discovery (OpenID Configuration) do realm.</summary>
    public Uri DiscoveryEndpoint => new(BaseAddress, $"/realms/{RealmName}/.well-known/openid-configuration");

    /// <summary><see cref="HttpClient"/> compartilhado para chamadas ao Keycloak (token, discovery, jwks).</summary>
    public HttpClient HttpClient => _httpClient ??= new HttpClient { BaseAddress = BaseAddress };

    /// <summary>
    /// Solicita um access token via Direct Access Grant (Resource Owner Password Credentials) ao client
    /// confidencial informado. Disponível APENAS no realm de teste — produção não habilita esse fluxo.
    /// </summary>
    /// <param name="username">Usuário do realm (ex.: <c>candidato</c>, <c>admin</c>).</param>
    /// <param name="password">Senha do usuário no realm-export.</param>
    /// <param name="clientId">Client confidencial a usar; default <see cref="ClientId"/>.</param>
    /// <returns>Access token compacto (JWS).</returns>
    public async Task<string> RequestAccessTokenAsync(
        string username,
        string password,
        string clientId = ClientId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);

        using HttpRequestMessage request = new(HttpMethod.Post, TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "password"),
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", ClientSecret),
                new KeyValuePair<string, string>("username", username),
                new KeyValuePair<string, string>("password", password),
            }),
        };

        using HttpResponseMessage response = await HttpClient.SendAsync(request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        TokenResponse? payload = await response.Content
            .ReadFromJsonAsync<TokenResponse>()
            .ConfigureAwait(false);

        if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            throw new InvalidOperationException(
                $"Resposta de token inválida do Keycloak para usuário '{username}'.");
        }

        return payload.AccessToken;
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync().ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        _httpClient?.Dispose();
        await _container.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Resolve o caminho absoluto do realm sintético de testes (<c>realm-e2e-tests.json</c>) caminhando
    /// para cima a partir do diretório de testes até encontrar a raiz do repositório (marcada por
    /// <c>UniPlus.slnx</c>). O canônico <c>realm-export.json</c> é deliberadamente ignorado — ele é
    /// fonte de verdade para dev/homologação/produção e não deve ser carregado em testes E2E.
    /// </summary>
    private static string ResolveRealmExportHostPath()
    {
        const string RealmFileName = "realm-e2e-tests.json";

        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            string candidate = Path.Combine(current.FullName, "docker", "keycloak", RealmFileName);
            if (File.Exists(candidate) && File.Exists(Path.Combine(current.FullName, "UniPlus.slnx")))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException(
            $"Não foi possível localizar 'docker/keycloak/{RealmFileName}' subindo a partir do diretório "
            + "de testes. Verifique se o teste está rodando dentro da árvore do repositório uniplus-api.");
    }

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "Instanciado via reflection por System.Text.Json em ReadFromJsonAsync.")]
    private sealed record TokenResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string AccessToken,
        [property: System.Text.Json.Serialization.JsonPropertyName("token_type")] string TokenType,
        [property: System.Text.Json.Serialization.JsonPropertyName("expires_in")] int ExpiresIn);
}
