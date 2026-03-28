namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.SharedKernel.Domain.Interfaces;

public interface IEditalRepository : IRepository<Edital>
{
    Task<Edital?> ObterComEtapasECotasAsync(Guid id, CancellationToken cancellationToken = default);
}
