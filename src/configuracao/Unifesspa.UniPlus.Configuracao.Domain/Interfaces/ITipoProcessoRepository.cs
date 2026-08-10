namespace Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Pagination;

public interface ITipoProcessoRepository
{
    Task<TipoProcesso?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);
    Task<TipoProcesso?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken);
    Task<(IReadOnlyList<TipoProcesso> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId, int limit, PaginationDirection direction, CancellationToken cancellationToken);
    Task AdicionarAsync(TipoProcesso tipo, CancellationToken cancellationToken);
    Task<bool> CodigoExisteAsync(string codigo, CancellationToken cancellationToken);
}
