namespace Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Pagination;

public interface ITipoConceitoRepository
{
    Task<TipoConceito?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);
    Task<TipoConceito?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken);
    Task<(IReadOnlyList<TipoConceito> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)> ListarPaginadoAsync(
        string? afterSortKey,
        Guid? afterId, int limit, PaginationDirection direction, CancellationToken cancellationToken);
    Task AdicionarAsync(TipoConceito tipo, CancellationToken cancellationToken);
    Task<IReadOnlyList<TipoConceito>> ListarPorCodigosAsync(IReadOnlyCollection<string> codigos, CancellationToken cancellationToken);
    Task<bool> CodigoExisteAsync(string codigo, CancellationToken cancellationToken);
}
