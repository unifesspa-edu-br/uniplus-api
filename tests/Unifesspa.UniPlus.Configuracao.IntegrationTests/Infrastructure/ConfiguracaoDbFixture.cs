namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;

using Testcontainers.PostgreSql;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Interceptors;
using Unifesspa.UniPlus.Monolito.Persistencia;

/// <summary>
/// Fixture xUnit que provisiona um Postgres efêmero (Testcontainers) com o
/// schema do <see cref="UniPlusDbContext"/> subido por <c>PrepararOBancoAsync</c> com as sementes do módulo,
/// e expõe uma factory de DbContext com os MESMOS interceptors de produção
/// (SoftDelete + Auditable). Cobre os cadastros Campus e LocalOferta (UNI-REQ #587).
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit IAsyncLifetime + IClassFixture<T> exigem tipo público.")]
[SuppressMessage(
    "Reliability",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Disposable resources released by IAsyncLifetime.DisposeAsync — xUnit invoca deterministicamente.")]
public sealed class ConfiguracaoDbFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("uniplus_configuracao_tests")
        .WithUsername("uniplus_test")
        .WithPassword("uniplus_test")
        .Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync().ConfigureAwait(false);

        await using UniPlusDbContext context = CreateDbContext(userId: null);
        await context.PrepararOBancoAsync().ConfigureAwait(false);
        await SementesDaConfiguracao.AplicarAsync(context, CancellationToken.None).ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Constrói um <see cref="UniPlusDbContext"/> com os interceptors de
    /// produção. Quando <paramref name="userId"/> é informado, simula um
    /// <c>IUserContext</c> autenticado; caso contrário, fallback <c>"system"</c>.
    /// </summary>
    public UniPlusDbContext CreateDbContext(string? userId)
    {
        StubUserContext? userContext = userId is null ? null : new StubUserContext(userId);

        DbContextOptions<MonolitoDbContext> options =
            new DbContextOptionsBuilder<MonolitoDbContext>()
                .UseNpgsql(ConnectionString)
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(
                    new SoftDeleteInterceptor(TimeProvider.System, userContext),
                    new AuditableInterceptor(TimeProvider.System, userContext))
                .Options;

        return new MonolitoDbContext(options);
    }
}
