namespace Unifesspa.UniPlus.Selecao.IntegrationTests.InstantesEmMicrossegundos;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

/// <summary>Sobe o MinIO e, depois dele, a API apontada para ele: o endereço do container entra na configuração da API.</summary>
public sealed class InstantesEmResiduoFixture : IAsyncLifetime
{
    private readonly MinioContainerFixture _minio = new();
    private ApiComPostgres? _api;

    public InstantesEmResiduoApiFactory Factory =>
        _api?.Factory ?? throw new InvalidOperationException("A API sobe em InitializeAsync, antes do primeiro teste.");

    public async Task InitializeAsync()
    {
        await _minio.InitializeAsync().ConfigureAwait(false);
        _api = new ApiComPostgres(_minio);
        await _api.InitializeAsync().ConfigureAwait(false);
        await UnidadesDeTesteNoCadastro.SemearAsync(_api.Factory.Services).ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        if (_api is not null)
        {
            await _api.DisposeAsync().ConfigureAwait(false);
        }

        await _minio.DisposeAsync().ConfigureAwait(false);
    }

    private sealed class ApiComPostgres(MinioContainerFixture minio) : MonolitoPostgresFixtureBase<InstantesEmResiduoApiFactory>
    {
        protected override InstantesEmResiduoApiFactory CreateFactory(string connectionString) => new(connectionString, minio);
    }
}
