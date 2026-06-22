namespace Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Repositório da entidade <see cref="ReferenciaReservaDemografica"/>
/// (ADR-0054: banco isolado <c>uniplus_configuracao</c>). Todas as leituras
/// excluem registros soft-deleted via query filter por convenção.
/// </summary>
public interface IReferenciaReservaDemograficaRepository
{
    /// <summary>Carrega a referência rastreada pelo contexto, para mutação.</summary>
    Task<ReferenciaReservaDemografica?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Carrega a referência para leitura (<c>AsNoTracking</c>) — projeção em DTO.</summary>
    Task<ReferenciaReservaDemografica?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Lista referências vivas paginadas por cursor keyset bidirecional (ADR-0026 +
    /// ADR-0089): ordena por <c>Id</c> (Guid v7, ADR-0032) e devolve as âncoras
    /// de <c>prev</c>/<c>next</c> (nulas quando não há aquele lado).
    /// </summary>
    Task<(IReadOnlyList<ReferenciaReservaDemografica> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken);

    Task AdicionarAsync(ReferenciaReservaDemografica referencia, CancellationToken cancellationToken);

    /// <summary>
    /// Marca a referência para remoção; o <c>SoftDeleteInterceptor</c> converte em
    /// soft-delete preenchendo <c>DeletedBy</c>/<c>DeletedAt</c>.
    /// </summary>
    void Remover(ReferenciaReservaDemografica referencia);

    /// <summary>Verifica se existe referência viva para o Censo informado (case-insensitive), excluindo opcionalmente um Id.</summary>
    Task<bool> CensoExisteEntreLivosAsync(string censoReferencia, Guid? excluirId, CancellationToken cancellationToken);
}
