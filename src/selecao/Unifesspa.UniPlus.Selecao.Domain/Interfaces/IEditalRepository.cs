namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

public interface IEditalRepository : IRepository<Edital>
{
    Task<Edital?> ObterComEtapasECotasAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista editais paginados por chave (ADR-0026): ordena por <c>Id</c>, retorna
    /// até <paramref name="take"/> itens cujo <c>Id</c> é maior que
    /// <paramref name="afterId"/>. <c>null</c> em <paramref name="afterId"/> retorna
    /// a primeira janela. Implementações devem aplicar <c>AsNoTracking</c> e
    /// honrar a ordenação de forma idempotente.
    /// </summary>
    Task<IReadOnlyList<Edital>> ListarPaginadoAsync(Guid? afterId, int take, CancellationToken cancellationToken = default);
}
