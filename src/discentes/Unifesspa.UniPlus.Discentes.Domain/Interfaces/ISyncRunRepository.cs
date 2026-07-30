using Unifesspa.UniPlus.Discentes.Domain.Entities;

namespace Unifesspa.UniPlus.Discentes.Domain.Interfaces;

public interface ISyncRunRepository
{
    Task<SyncRun?> ObterSyncRunAsync(Guid id, CancellationToken ct = default);
    Task AdicionarSyncRunAsync(SyncRun entity, CancellationToken ct = default);
    void AtualizarSyncRun(SyncRun entity);
}
