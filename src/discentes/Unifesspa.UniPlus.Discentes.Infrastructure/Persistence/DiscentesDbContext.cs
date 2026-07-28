namespace Unifesspa.UniPlus.Discentes.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Discentes.Application.Abstractions;
using Unifesspa.UniPlus.Discentes.Domain.Entities;
using Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.Records;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// <see cref="DbContext"/> do módulo Discentes — banco/schema <c>discentes</c>, naming
/// snake_case (ADR-0054). Hospeda a réplica de <see cref="VinculoDiscente"/> (mapeada via
/// <see cref="VinculoDiscenteRecord"/>, ADR-0121) e o controle de execução de sincronização
/// (<see cref="SyncRun"/>).
/// </summary>
public sealed class DiscentesDbContext : DbContext, IDiscentesUnitOfWork
{
    /// <summary>
    /// Schema do módulo no banco único do monólito modular.
    /// </summary>
    public const string Schema = "discentes";

    public DiscentesDbContext(DbContextOptions<DiscentesDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Modelo de persistência da réplica de vínculos — CPF cifrado em repouso
    /// (ADR-0121); acesso de domínio somente via <c>IVinculoDiscenteRepository</c>.
    /// </summary>
    public DbSet<VinculoDiscenteRecord> VinculosDiscentes => Set<VinculoDiscenteRecord>();

    public DbSet<SyncRun> SyncRuns => Set<SyncRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DiscentesDbContext).Assembly);
        modelBuilder.AplicarFiltroGlobalSoftDelete();

        base.OnModelCreating(modelBuilder);
    }

    public async Task<int> SalvarAlteracoesAsync(CancellationToken cancellationToken = default)
    {
        return await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
