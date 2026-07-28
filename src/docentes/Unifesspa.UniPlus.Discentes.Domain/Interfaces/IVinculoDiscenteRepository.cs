using Unifesspa.UniPlus.Discentes.Domain.Entities;

namespace Unifesspa.UniPlus.Discentes.Domain.Interfaces;

public interface IVinculoDiscenteRepository
{
    Task<VinculoDiscente?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<VinculoDiscente?> GetByIdSigaaAsync(long idDiscenteSigaa, CancellationToken cancellationToken = default);
    Task AddAsync(VinculoDiscente entity, CancellationToken cancellationToken = default);
    void Update(VinculoDiscente entity);
}
