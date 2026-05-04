namespace Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

using System.Net;
using System.Net.Http.Json;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

/// <summary>
/// Sobe um container HashiCorp Vault em modo dev via Testcontainers, habilita o transit secrets engine
/// e expõe endereço e token root para uso em testes de integração.
/// Compartilhada via <c>[Collection("Vault")]</c> — cada assembly que a usa deve declarar sua própria
/// <c>[CollectionDefinition]</c> com o mesmo nome (ver padrão <c>KeycloakCollection</c>).
/// </summary>
public sealed class VaultContainerFixture : IAsyncLifetime
{
    public const string Image = "hashicorp/vault:1.18";
    public const string RootToken = "root";
    public const string TransitMount = "transit";
    public const string CollectionName = "Vault";

    private const ushort VaultPort = 8200;

    private readonly IContainer _container;

    public VaultContainerFixture()
    {
        _container = new ContainerBuilder(Image)
            .WithPortBinding(VaultPort, true)
            .WithEnvironment("VAULT_DEV_ROOT_TOKEN_ID", RootToken)
            .WithEnvironment("VAULT_DEV_LISTEN_ADDRESS", $"0.0.0.0:{VaultPort}")
            .WithCommand("server", "-dev")
            .WithWaitStrategy(
                Wait.ForUnixContainer()
                    .UntilHttpRequestIsSucceeded(r => r
                        .ForPort(VaultPort)
                        .ForPath("/v1/sys/health")
                        .ForStatusCode(HttpStatusCode.OK)))
            .Build();
    }

    /// <summary>Endereço HTTP do Vault no host com porta efêmera publicada.</summary>
    public string VaultAddress =>
        $"http://{_container.Hostname}:{_container.GetMappedPublicPort(VaultPort)}";

    /// <summary>
    /// Cria uma chave de transit no Vault, se ainda não existir.
    /// Idempotente — pode ser chamado por múltiplos testes com o mesmo nome.
    /// </summary>
    public async Task EnsureKeyExistsAsync(string keyName, string keyType = "aes256-gcm96")
    {
        using HttpClient client = CreateAdminClient();
        HttpResponseMessage response = await client
            .PostAsJsonAsync($"/v1/{TransitMount}/keys/{keyName}", new { type = keyType })
            .ConfigureAwait(false);

        // 200 (criada) ou 400 (já existe) são ambos aceitáveis
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.BadRequest)
        {
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            throw new InvalidOperationException(
                $"Falha ao criar chave '{keyName}' no Vault: {response.StatusCode} — {body}");
        }
    }

    /// <summary>
    /// Retorna um <see cref="HttpClient"/> pré-configurado com o token root para chamadas administrativas
    /// ao Vault. O chamador é responsável pelo dispose.
    /// </summary>
    public HttpClient CreateAdminClient()
    {
        HttpClient client = new() { BaseAddress = new Uri(VaultAddress) };
        client.DefaultRequestHeaders.Add("X-Vault-Token", RootToken);
        return client;
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync().ConfigureAwait(false);
        await EnableTransitEngineAsync().ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync().ConfigureAwait(false);
    }

    private async Task EnableTransitEngineAsync()
    {
        using HttpClient client = CreateAdminClient();
        HttpResponseMessage response = await client
            .PostAsJsonAsync("/v1/sys/mounts/transit", new { type = "transit" })
            .ConfigureAwait(false);

        // 400 com "path is already in use" significa que já está habilitado (dev mode pode pré-habilitar)
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.BadRequest)
        {
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            throw new InvalidOperationException(
                $"Falha ao habilitar transit engine no Vault: {response.StatusCode} — {body}");
        }
    }
}
