namespace Unifesspa.UniPlus.SharedKernel.Domain.Interfaces;

public interface IUnitOfWork
{
    Task<int> SalvarAlteracoesAsync(CancellationToken cancellationToken = default);
}
