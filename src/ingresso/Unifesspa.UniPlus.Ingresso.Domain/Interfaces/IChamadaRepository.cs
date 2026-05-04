namespace Unifesspa.UniPlus.Ingresso.Domain.Interfaces;

using Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

public interface IChamadaRepository : IRepository<Chamada>
{
    Task<Chamada?> ObterComConvocacoesAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> ObterProximoNumeroChamadaAsync(Guid editalId, CancellationToken cancellationToken = default);
}
