namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;

using Testcontainers.PostgreSql;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Interceptors;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence;

/// <summary>
/// Fixture xUnit que provisiona um Postgres efêmero (Testcontainers) com o schema
/// do <see cref="PublicacoesDbContext"/> aplicado via <c>MigrateAsync</c>, e expõe
/// uma factory de DbContext com os MESMOS interceptors de produção
/// (SoftDelete + Auditable).
/// </summary>
/// <remarks>
/// O container sobe cru: nada do <c>init-db</c> roda aqui. Se a extensão
/// <c>btree_gist</c> não nascesse na migration, a exclusion constraint falharia
/// já no <c>MigrateAsync</c> — o que torna esta fixture, por si, a prova de que
/// a extensão está no lugar certo.
/// </remarks>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit IAsyncLifetime + ICollectionFixture<T> exigem tipo público.")]
[SuppressMessage(
    "Reliability",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Disposable resources released by IAsyncLifetime.DisposeAsync — xUnit invoca deterministicamente.")]
public sealed class PublicacoesDbFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("uniplus_publicacoes_tests")
        .WithUsername("uniplus_test")
        .WithPassword("uniplus_test")
        .Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync().ConfigureAwait(false);

        await using PublicacoesDbContext context = CreateDbContext(userId: null);
        await context.Database.MigrateAsync().ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Constrói um <see cref="PublicacoesDbContext"/> com os interceptors de
    /// produção. Quando <paramref name="userId"/> é informado, simula um
    /// <c>IUserContext</c> autenticado; caso contrário, fallback <c>"system"</c>.
    /// </summary>
    public PublicacoesDbContext CreateDbContext(string? userId)
    {
        StubUserContext? userContext = userId is null ? null : new StubUserContext(userId);

        DbContextOptions<PublicacoesDbContext> options =
            new DbContextOptionsBuilder<PublicacoesDbContext>()
                .UseNpgsql(ConnectionString)
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(
                    new SoftDeleteInterceptor(TimeProvider.System, userContext),
                    new AuditableInterceptor(TimeProvider.System, userContext))
                .Options;

        return new PublicacoesDbContext(options);
    }
}
