namespace Unifesspa.UniPlus.Selecao.IntegrationTests.RolDeRegras;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;

using Testcontainers.PostgreSql;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Interceptors;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// Fixture xUnit que provisiona um Postgres efêmero (Testcontainers) com o
/// schema do <see cref="SelecaoDbContext"/> aplicado via <c>MigrateAsync</c> —
/// inclui a migration <c>AddRolDeRegras</c> (Story #772) e o seed das 18
/// regras <c>v1</c>, validando ponta-a-ponta contra Postgres real.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit IAsyncLifetime + IClassFixture<T> exigem tipo público.")]
[SuppressMessage(
    "Reliability",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Disposable resources released by IAsyncLifetime.DisposeAsync — xUnit invoca deterministicamente.")]
public sealed class RegraCatalogoDbFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("uniplus_rol_de_regras_tests")
        .WithUsername("uniplus_test")
        .WithPassword("uniplus_test")
        .Build();

    private string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync().ConfigureAwait(false);

        await using SelecaoDbContext context = CreateDbContext();
        await context.Database.MigrateAsync().ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync().ConfigureAwait(false);
    }

    public SelecaoDbContext CreateDbContext()
    {
        DbContextOptions<SelecaoDbContext> options = new DbContextOptionsBuilder<SelecaoDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(
                new SoftDeleteInterceptor(TimeProvider.System, userContext: null),
                new AuditableInterceptor(TimeProvider.System, userContext: null))
            .Options;

        return new SelecaoDbContext(options);
    }
}
