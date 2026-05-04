namespace Unifesspa.UniPlus.Ingresso.Domain.Interfaces;

using Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

public interface IMatriculaRepository : IRepository<Matricula>
{
    Task<Matricula?> ObterComDocumentosAsync(Guid id, CancellationToken cancellationToken = default);
}
