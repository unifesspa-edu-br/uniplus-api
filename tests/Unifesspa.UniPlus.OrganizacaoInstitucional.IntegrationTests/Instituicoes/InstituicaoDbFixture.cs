namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Instituicoes;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;

using Testcontainers.PostgreSql;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Interceptors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;
using Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Unidades;

/// <summary>
/// Fixture xUnit que provisiona um Postgres efêmero (Testcontainers) com o
/// schema do <see cref="OrganizacaoInstitucionalDbContext"/> aplicado via
/// <c>MigrateAsync</c>, e expõe uma factory de DbContext com os MESMOS
/// interceptors da produção (SoftDelete + Auditable).
/// </summary>
/// <remarks>
/// Story #585 — valida ponta-a-ponta o invariante singleton da
/// <c>Instituicao</c>: índice único parcial sobre a coluna sentinela
/// (<c>WHERE is_deleted = false</c>), soft-delete liberando o slot, FK intra-banco
/// <c>unidade_raiz_id → unidade(id)</c> e audit trail.
/// </remarks>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit IAsyncLifetime + IClassFixture<T> exigem tipo público.")]
[SuppressMessage(
    "Reliability",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Disposable resources released by IAsyncLifetime.DisposeAsync — xUnit invoca deterministicamente.")]
public sealed class InstituicaoDbFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("uniplus_instituicao_tests")
        .WithUsername("uniplus_test")
        .WithPassword("uniplus_test")
        .Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync().ConfigureAwait(false);

        await using OrganizacaoInstitucionalDbContext context = CreateDbContext(userId: null);
        await context.Database.MigrateAsync().ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Constrói um <see cref="OrganizacaoInstitucionalDbContext"/> com os
    /// interceptors de produção. Quando <paramref name="userId"/> é informado,
    /// simula um <c>IUserContext</c> autenticado; caso contrário, os interceptors
    /// usam o fallback <c>"system"</c>.
    /// </summary>
    public OrganizacaoInstitucionalDbContext CreateDbContext(string? userId)
    {
        StubUserContext? userContext = userId is null ? null : new StubUserContext(userId);

        DbContextOptions<OrganizacaoInstitucionalDbContext> options =
            new DbContextOptionsBuilder<OrganizacaoInstitucionalDbContext>()
                .UseNpgsql(ConnectionString)
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(
                    new SoftDeleteInterceptor(TimeProvider.System, userContext),
                    new AuditableInterceptor(TimeProvider.System, userContext))
                .Options;

        return new OrganizacaoInstitucionalDbContext(options);
    }
}
