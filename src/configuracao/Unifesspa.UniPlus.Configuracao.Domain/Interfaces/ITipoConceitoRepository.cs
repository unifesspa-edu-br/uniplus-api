namespace Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Pagination;

public interface ITipoConceitoRepository
{
    Task<TipoConceito?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);
    Task<TipoConceito?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken);
    Task<(IReadOnlyList<TipoConceito> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId, int limit, PaginationDirection direction, CancellationToken cancellationToken);
    Task AdicionarAsync(TipoConceito tipo, CancellationToken cancellationToken);
    Task<bool> CodigoExisteAsync(string codigo, CancellationToken cancellationToken);
}
