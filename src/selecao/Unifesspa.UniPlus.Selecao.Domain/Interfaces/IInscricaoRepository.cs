namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

public interface IInscricaoRepository : IRepository<Inscricao>
{
    Task<bool> ExisteInscricaoAtivaAsync(Guid candidatoId, Guid editalId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Inscricao>> ObterPorEditalAsync(Guid editalId, CancellationToken cancellationToken = default);
}
