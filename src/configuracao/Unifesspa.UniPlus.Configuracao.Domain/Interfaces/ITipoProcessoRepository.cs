namespace Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Pagination;

public interface ITipoProcessoRepository
{
    Task<TipoProcesso?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);
    Task<TipoProcesso?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken);
    /// <summary>
    /// Lista o cadastro paginado por cursor. <paramref name="apenasAtivos"/> distingue a
    /// leitura pública (só ativos, UNI-REQ-0098) da visão de manutenção, que precisa
    /// enxergar o desativado para poder reativá-lo.
    /// </summary>
    Task<(IReadOnlyList<TipoProcesso> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId, int limit, PaginationDirection direction, bool apenasAtivos, CancellationToken cancellationToken);
    Task AdicionarAsync(TipoProcesso tipo, CancellationToken cancellationToken);
    Task<bool> CodigoExisteAsync(string codigo, CancellationToken cancellationToken);
}
