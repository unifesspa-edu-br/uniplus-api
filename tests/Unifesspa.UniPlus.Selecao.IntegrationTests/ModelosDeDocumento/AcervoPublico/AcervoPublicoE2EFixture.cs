namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ModelosDeDocumento.AcervoPublico;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// Sobe o MinIO com o acervo público provisionado e, só depois, a API apontada para ele — a ordem
/// importa porque o endereço do container entra na configuração da API.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit ICollectionFixture<T> requires the fixture type to be public.")]
public sealed class AcervoPublicoE2EFixture : IAsyncLifetime
{
    private readonly MinioContainerFixture _minio = new();
    private ApiComPostgres? _api;

    public AcervoPublicoApiFactory Factory =>
        _api?.Factory ?? throw new InvalidOperationException("A API sobe em InitializeAsync, antes do primeiro teste.");

    public async Task InitializeAsync()
    {
        await _minio.InitializeAsync().ConfigureAwait(false);
        await _minio.ProvisionarAcervoPublicoAsync().ConfigureAwait(false);

        _api = new ApiComPostgres(_minio);
        await _api.InitializeAsync().ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        if (_api is not null)
        {
            await _api.DisposeAsync().ConfigureAwait(false);
        }

        await _minio.DisposeAsync().ConfigureAwait(false);
    }

    private sealed class ApiComPostgres(MinioContainerFixture minio) : MonolitoPostgresFixtureBase<AcervoPublicoApiFactory>
    {
        protected override AcervoPublicoApiFactory CreateFactory(string connectionString) => new(connectionString, minio);
    }
}
