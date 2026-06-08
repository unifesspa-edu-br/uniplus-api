namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Unidades;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;

using Testcontainers.PostgreSql;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Interceptors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;

/// <summary>
/// Fixture xUnit que provisiona um Postgres efêmero (Testcontainers) com o
/// schema do <see cref="OrganizacaoInstitucionalDbContext"/> aplicado via
/// <c>MigrateAsync</c>, e expõe uma factory de DbContext com os MESMOS
/// interceptors da produção (SoftDelete + Auditable).
/// </summary>
/// <remarks>
/// Story #586 — valida ponta-a-ponta:
/// <list type="bullet">
///   <item>UNIQUE parcial sobre slug/sigla/codigo (WHERE is_deleted = false).</item>
///   <item>Soft-delete via SoftDeleteInterceptor liberta o slot único.</item>
///   <item>Histórico de identificadores criado no Insert via EF.</item>
///   <item>Audit trail (created_by / updated_by) preenchido pelo AuditableInterceptor.</item>
///   <item>FK hierárquica unidade_superior_id com ReferentialAction.Restrict.</item>
/// </list>
/// </remarks>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit IAsyncLifetime + IClassFixture<T> exigem tipo público.")]
[SuppressMessage(
    "Reliability",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Disposable resources released by IAsyncLifetime.DisposeAsync — xUnit invoca deterministicamente.")]
public sealed class UnidadeDbFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("uniplus_unidade_tests")
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
    /// simula um <c>IUserContext</c> autenticado — caso contrário, os
    /// interceptors usam o fallback <c>"system"</c>.
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
