namespace Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Discentes.Domain.Entities;
using Unifesspa.UniPlus.Discentes.Domain.Interfaces;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em DiscentesInfrastructureRegistration.")]
public sealed class SyncRunRepository : ISyncRunRepository
{
    private readonly DiscentesDbContext _dbContext;

    public SyncRunRepository(DiscentesDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<SyncRun?> ObterSyncRunAsync(Guid id, CancellationToken ct = default)
    {
        return _dbContext.SyncRuns.FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task AdicionarSyncRunAsync(SyncRun entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await _dbContext.SyncRuns.AddAsync(entity, ct).ConfigureAwait(false);
    }

    public void AtualizarSyncRunAsync(SyncRun entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _dbContext.SyncRuns.Update(entity);
    }
}
