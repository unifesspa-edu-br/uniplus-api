namespace Unifesspa.UniPlus.SharedKernel.Domain.Interfaces;

using Unifesspa.UniPlus.SharedKernel.Domain.Entities;

public interface IRepository<T> where T : EntityBase
{
    Task<T?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> ObterTodosAsync(CancellationToken cancellationToken = default);
    Task AdicionarAsync(T entity, CancellationToken cancellationToken = default);
    void Atualizar(T entity);
    void Remover(T entity);
}
