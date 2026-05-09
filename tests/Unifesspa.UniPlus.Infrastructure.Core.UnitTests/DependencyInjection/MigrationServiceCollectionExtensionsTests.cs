namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.DependencyInjection;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

public sealed class MigrationServiceCollectionExtensionsTests
{
    [Fact]
    public async Task ApplyMigrationsAsync_ServicesNulo_LancaArgumentNullException()
    {
        IServiceProvider? services = null;

        Func<Task> acao = async () => await services!.ApplyMigrationsAsync<DummyDbContext>();

        await acao.Should().ThrowAsync<ArgumentNullException>();
    }

    // Comportamento end-to-end (banco relacional + migrations idempotente + advisory lock entre
    // réplicas) é validado por integração — ver MigrationServiceCollectionExtensionsIntegrationTests
    // em Unifesspa.UniPlus.Infrastructure.Core.IntegrationTests/Migrations/. InMemory provider não
    // suporta GetPendingMigrationsAsync/MigrateAsync, então o caminho relacional precisa de Postgres.

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "Type marker para o método genérico — nunca instanciado.")]
    private sealed class DummyDbContext : DbContext
    {
    }
}
