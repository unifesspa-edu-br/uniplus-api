namespace Unifesspa.UniPlus.Ingresso.Domain.Interfaces;

using Unifesspa.UniPlus.Ingresso.Domain.Entities;
using Unifesspa.UniPlus.SharedKernel.Domain.Interfaces;

public interface IMatriculaRepository : IRepository<Matricula>
{
    Task<Matricula?> ObterComDocumentosAsync(Guid id, CancellationToken cancellationToken = default);
}
