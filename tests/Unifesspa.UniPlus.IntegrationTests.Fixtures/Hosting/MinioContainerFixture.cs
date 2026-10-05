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
/// testes e runtime. A imagem vem do espelho no registry da organização, fixada por digest,
/// porque os registries do MinIO deixaram de servi-la publicamente.
/// </remarks>
public sealed class MinioContainerFixture : IAsyncLifetime
{
    public const string Image = "ghcr.io/unifesspa-edu-br/minio:RELEASE.2025-09-07T16-13-09Z@sha256:52dfd5c0bbd38d3219f2058c7af216d9f9a27a994b7b5baad09bbd38866015ff";
    public const string AccessKey = "minioadmin";
    public const string SecretKey = "minioadmin";
    public const string CollectionName = "Minio";

    /// <summary>O bucket do acervo público, com o nome que a política versionada nomeia.</summary>
    public const string BucketDoAcervoPublico = "uniplus-acervo-publico";

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
                    // /minio/health/ready (não /live): live só confirma que o processo subiu, mas a
                    // object layer pode ainda não servir a API S3 — ListBucketsAsync intermitente
                    // retornava Unhealthy logo após o start (corrida liveness x readiness, #718).
                    // O probe de readiness só passa quando o servidor já atende requisições.
                    .UntilHttpRequestIsSucceeded(r => r
                        .ForPort(ApiPort)
                        .ForPath("/minio/health/ready")))
            .Build();
    }

    /// <summary>Endpoint MinIO no formato <c>host:port</c> (sem esquema). Apto a alimentar <c>Storage:Endpoint</c>.</summary>
    public string Endpoint =>
        $"{_container.Hostname}:{_container.GetMappedPublicPort(ApiPort)}";

    /// <summary>
    /// Endereço do acervo público pela porta de dados do container, para os testes lerem o objeto
    /// como o cidadão o lê — anonimamente. Fora daqui o acervo é servido pela borda (ADR-0132).
    /// </summary>
    public string EnderecoDoAcervoPublico => $"http://{Endpoint}/{BucketDoAcervoPublico}";

    public Task InitializeAsync() => _container.StartAsync();

    /// <summary>
    /// Cria o bucket do acervo público e aplica a política de leitura anônima versionada em
    /// <c>docker/minio/acervo-publico.policy.json</c>, pelos mesmos comandos do docker-compose — a
    /// aplicação nunca cria esse bucket. Idempotente.
    /// </summary>
    public async Task ProvisionarAcervoPublicoAsync()
    {
        const string CaminhoNoContainer = "/tmp/acervo-publico.policy.json";
        byte[] politica = await File.ReadAllBytesAsync(
            Path.Join(AppContext.BaseDirectory, "Hosting", "acervo-publico.policy.json")).ConfigureAwait(false);
        await _container.CopyAsync(politica, CaminhoNoContainer).ConfigureAwait(false);

        await ExecutarMcAsync("alias", "set", "local", $"http://localhost:{ApiPort}", AccessKey, SecretKey).ConfigureAwait(false);
        await ExecutarMcAsync("mb", "--ignore-existing", $"local/{BucketDoAcervoPublico}").ConfigureAwait(false);
        await ExecutarMcAsync("anonymous", "set-json", CaminhoNoContainer, $"local/{BucketDoAcervoPublico}").ConfigureAwait(false);
    }

    private async Task ExecutarMcAsync(params string[] argumentos)
    {
        ExecResult resultado = await _container.ExecAsync(["mc", .. argumentos]).ConfigureAwait(false);
        if (resultado.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"mc {string.Join(' ', argumentos.Take(2))} falhou ({resultado.ExitCode}): {resultado.Stderr}");
        }
    }

    public async Task DisposeAsync() =>
        await _container.DisposeAsync().ConfigureAwait(false);
}
