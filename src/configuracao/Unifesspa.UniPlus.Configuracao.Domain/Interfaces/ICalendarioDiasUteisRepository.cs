namespace Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Repositório da entidade <see cref="CalendarioDiasUteis"/> (ADR-0054: banco isolado
/// <c>uniplus_configuracao</c>). Todas as leituras excluem registros soft-deleted via
/// query filter por convenção.
/// </summary>
public interface ICalendarioDiasUteisRepository
{
    /// <summary>Carrega o dataset rastreado pelo contexto (com <see cref="CalendarioDiasUteis.DiasNaoUteis"/>), para mutação.</summary>
    Task<CalendarioDiasUteis?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Carrega o dataset para leitura (<c>AsNoTracking</c>) — projeção em DTO.</summary>
    Task<CalendarioDiasUteis?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Lista datasets vivos paginados por cursor keyset bidirecional (ADR-0026 +
    /// ADR-0089): ordena por <c>Id</c> (Guid v7, ADR-0032) e devolve as âncoras
    /// de <c>prev</c>/<c>next</c> (nulas quando não há aquele lado).
    /// </summary>
    Task<(IReadOnlyList<CalendarioDiasUteis> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken);

    /// <summary>Carrega o dataset vigente, rastreado, se houver — para o handler desmarcá-lo ao trocar de vigente.</summary>
    Task<CalendarioDiasUteis?> ObterVigenteAsync(CancellationToken cancellationToken);

    Task AdicionarAsync(CalendarioDiasUteis calendario, CancellationToken cancellationToken);

    /// <summary>
    /// Marca o dataset para remoção; o <c>SoftDeleteInterceptor</c> converte em
    /// soft-delete preenchendo <c>DeletedBy</c>/<c>DeletedAt</c>.
    /// </summary>
    void Remover(CalendarioDiasUteis calendario);
}
