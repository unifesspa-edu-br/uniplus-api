namespace Unifesspa.UniPlus.Infrastructure.Core.IntegrationTests.Migrations;

using AwesomeAssertions;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Testcontainers.PostgreSql;

using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Convenção xUnit para classes de teste.")]
public sealed class MigrationServiceCollectionExtensionsIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("uniplus_migration_tests")
        .WithUsername("uniplus_test")
        .WithPassword("uniplus_test")
        .Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task ApplyMigrationsAsync_BancoVazioSemMigrationsDefinidas_NaoFalha()
    {
        // Estado de scaffolding atual dos módulos Uni+: DbContexts existem mas sem migrations
        // produtivas registradas. ApplyMigrationsAsync deve lidar com array vazio gracefully —
        // não conectar tentar `Migrate()` quando não há nada a aplicar.
        ServiceCollection services = new();
        services.AddLogging();
        services.AddDbContext<TestDbContext>(opts => opts.UseNpgsql(_postgres.GetConnectionString()));

        await using ServiceProvider sp = services.BuildServiceProvider();

        Func<Task> acao = async () => await sp.ApplyMigrationsAsync<TestDbContext>();

        await acao.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ApplyMigrationsAsync_ChamadaIdempotenteEntreRéplicas_NaoCorrompeBanco()
    {
        // Simula 2 réplicas startando simultâneo. EF Core usa advisory lock para coordenar:
        // primeira aplica, segunda detecta que já foi aplicado e retorna gracefully.
        ServiceCollection services = new();
        services.AddLogging();
        services.AddDbContext<TestDbContext>(opts => opts.UseNpgsql(_postgres.GetConnectionString()));

        await using ServiceProvider sp = services.BuildServiceProvider();

        Task r1 = sp.ApplyMigrationsAsync<TestDbContext>();
        Task r2 = sp.ApplyMigrationsAsync<TestDbContext>();

        Func<Task> acao = async () => await Task.WhenAll(r1, r2);

        await acao.Should().NotThrowAsync();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "Instanciada via DI (AddDbContext) por reflection.")]
    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
    }
}
