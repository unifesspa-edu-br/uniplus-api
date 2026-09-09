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

    /// <summary>
    /// Registra explicitamente no <c>ChangeTracker</c> um <see cref="DiaNaoUtil"/>
    /// recém-incluído num <see cref="CalendarioDiasUteis"/> já rastreado
    /// (<see cref="CalendarioDiasUteis.IncluirDiaNaoUtil"/>, api#1458).
    /// </summary>
    /// <remarks>
    /// Necessário porque o <c>ChangeTracker</c> não descobre sozinho um item
    /// adicionado à coleção-campo <c>_diasNaoUteis</c> de um agregado que já
    /// estava <c>Unchanged</c> antes da inclusão — comprovado empiricamente: o
    /// <c>DetectChanges</c> automático (disparado por <c>SaveChangesAsync</c>)
    /// não cria uma <c>EntityEntry</c> para o novo filho nesse cenário, e a
    /// inclusão silenciosamente não persiste nada. Diferente de
    /// <see cref="AdicionarAsync"/> (que atacha o agregado INTEIRO, ainda não
    /// rastreado, e o EF Core caminha o grafo completo ao processar o <c>Add</c>),
    /// aqui o pai já está rastreado — só o filho novo precisa do <c>Add</c>
    /// explícito.
    /// </remarks>
    void AdicionarDiaNaoUtil(DiaNaoUtil dia);
}
