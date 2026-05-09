namespace Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

/// <summary>
/// Sobe um container MinIO via Testcontainers em modo single-node, expondo endpoint, credentials root
/// e SSL=off para uso em testes de integração. Compartilhada via <c>[Collection("Minio")]</c> — cada
/// assembly que a usa deve declarar sua própria <c>[CollectionDefinition]</c> com o mesmo nome
/// (ver padrão <see cref="VaultContainerFixture"/>).
/// </summary>
/// <remarks>
/// A imagem é fixada na mesma RELEASE usada pelo <c>docker-compose.yml</c> e pelo bootstrap
/// standalone — alinhar a tag aqui com produção evita variações de schema/comportamento entre
/// testes e runtime.
/// </remarks>
public sealed class MinioContainerFixture : IAsyncLifetime
{
    public const string Image = "minio/minio:RELEASE.2025-09-07T16-13-09Z";
    public const string AccessKey = "minioadmin";
    public const string SecretKey = "minioadmin";
    public const string CollectionName = "Minio";

    private const ushort ApiPort = 9000;
    private const ushort ConsolePort = 9001;

    private readonly IContainer _container;

    public MinioContainerFixture()
    {
        _container = new ContainerBuilder(Image)
            .WithPortBinding(ApiPort, true)
            .WithPortBinding(ConsolePort, true)
            .WithEnvironment("MINIO_ROOT_USER", AccessKey)
            .WithEnvironment("MINIO_ROOT_PASSWORD", SecretKey)
            .WithCommand("server", "/data", "--console-address", $":{ConsolePort}")
            .WithWaitStrategy(
                Wait.ForUnixContainer()
                    .UntilHttpRequestIsSucceeded(r => r
                        .ForPort(ApiPort)
                        .ForPath("/minio/health/live")))
            .Build();
    }

    /// <summary>Endpoint MinIO no formato <c>host:port</c> (sem esquema). Apto a alimentar <c>Storage:Endpoint</c>.</summary>
    public string Endpoint =>
        $"{_container.Hostname}:{_container.GetMappedPublicPort(ApiPort)}";

    public Task InitializeAsync() => _container.StartAsync();

    public async Task DisposeAsync() =>
        await _container.DisposeAsync().ConfigureAwait(false);
}
