using Unifesspa.UniPlus.Discentes.Domain.Entities;

namespace Unifesspa.UniPlus.Discentes.Domain.Interfaces;

public interface ISyncRunRepository
{
    Task<SyncRun?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(SyncRun entity, CancellationToken ct = default);
    void Update(SyncRun entity);
}
