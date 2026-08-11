namespace Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Pagination;

public interface ITipoEtapaRepository
{
    Task<TipoEtapa?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);
    Task<TipoEtapa?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken);
    Task<(IReadOnlyList<TipoEtapa> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId, int limit, PaginationDirection direction, CancellationToken cancellationToken);
    Task AdicionarAsync(TipoEtapa tipo, CancellationToken cancellationToken);
    Task<bool> CodigoExisteAsync(string codigo, CancellationToken cancellationToken);
}
