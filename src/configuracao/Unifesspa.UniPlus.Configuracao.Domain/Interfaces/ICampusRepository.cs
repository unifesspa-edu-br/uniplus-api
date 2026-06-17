namespace Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Repositório da entidade <see cref="Campus"/> (ADR-0054: banco isolado
/// <c>uniplus_configuracao</c>). Todas as leituras excluem registros
/// soft-deleted via query filter por convenção.
/// </summary>
public interface ICampusRepository
{
    /// <summary>Carrega o campus rastreado pelo contexto, para mutação.</summary>
    Task<Campus?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Carrega o campus para leitura (<c>AsNoTracking</c>) — projeção em DTO.</summary>
    Task<Campus?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Lista campi vivos paginados por cursor keyset bidirecional (ADR-0026 +
    /// ADR-0089): ordena por <c>Id</c> (Guid v7, ADR-0032) e devolve as âncoras
    /// de <c>prev</c>/<c>next</c> (nulas quando não há aquele lado).
    /// </summary>
    Task<(IReadOnlyList<Campus> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken);

    Task AdicionarAsync(Campus campus, CancellationToken cancellationToken);

    /// <summary>
    /// Marca o campus para remoção; o <c>SoftDeleteInterceptor</c> converte em
    /// soft-delete preenchendo <c>DeletedBy</c>/<c>DeletedAt</c>.
    /// </summary>
    void Remover(Campus campus);

    /// <summary>Verifica se existe campus vivo com a sigla informada (case-insensitive), excluindo opcionalmente um Id.</summary>
    Task<bool> SiglaExisteEntreLivosAsync(string sigla, Guid? excluirId, CancellationToken cancellationToken);

    /// <summary>Verifica se existe campus vivo com o Id informado — usado por LocalOferta para conferir o campus responsável.</summary>
    Task<bool> ExisteVivoAsync(Guid id, CancellationToken cancellationToken);
}
