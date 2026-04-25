namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Infrastructure;

using System.Diagnostics.CodeAnalysis;

using Testcontainers.PostgreSql;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit 2.x IClassFixture<T> requires the fixture type to be public.")]
[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Lifecycle ownership is via xUnit IAsyncLifetime — analyzer não reconhece este contrato.")]
[SuppressMessage(
    "Usage",
    "CA2213:Disposable fields should be disposed",
    Justification = "Container é disposed em IAsyncLifetime.DisposeAsync — analyzer não reconhece este caminho.")]
public sealed class SelecaoApiFactory : ApiFactoryBase<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("uniplus_tests")
        .WithUsername("uniplus")
        .WithPassword("uniplus_dev")
        .Build();

    private string? _connectionString;

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _container.StartAsync().ConfigureAwait(false);
        _connectionString = _container.GetConnectionString();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync().ConfigureAwait(false);
        await _container.DisposeAsync().ConfigureAwait(false);
    }

    protected override IEnumerable<KeyValuePair<string, string?>> GetConfigurationOverrides() =>
    [
        new("ConnectionStrings:SelecaoDb", _connectionString
            ?? throw new InvalidOperationException("Postgres container ainda não inicializado — InitializeAsync precisa rodar antes.")),
        new("Auth:Authority", "http://localhost/test-realm"),
        new("Auth:Audience", "uniplus"),
    ];
}
