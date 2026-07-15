namespace Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Repositório da entidade <see cref="PrecedenciaFase"/> (ADR-0054: banco isolado
/// <c>uniplus_configuracao</c>). Todas as leituras excluem registros soft-deleted
/// via query filter por convenção.
/// </summary>
public interface IPrecedenciaFaseRepository
{
    /// <summary>Carrega a aresta rastreada pelo contexto, para mutação.</summary>
    Task<PrecedenciaFase?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Carrega a aresta para leitura (<c>AsNoTracking</c>) — projeção em DTO.</summary>
    Task<PrecedenciaFase?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Lista arestas vivas paginadas por cursor keyset bidirecional (ADR-0026 +
    /// ADR-0089): ordena por <c>Id</c> (Guid v7, ADR-0032) e devolve as âncoras de
    /// <c>prev</c>/<c>next</c> (nulas quando não há aquele lado).
    /// </summary>
    Task<(IReadOnlyList<PrecedenciaFase> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lista <b>todas</b> as arestas vivas, sem paginação — o grafo vigente que a
    /// factory <see cref="PrecedenciaFase.Criar"/> recebe como parâmetro para as
    /// guardas de self-loop, duplicata e ciclo (ADR-0042: grafo injetado, domínio
    /// não navega/consulta).
    /// </summary>
    Task<IReadOnlyList<PrecedenciaFase>> ListarVivasAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Serializa a seção crítica leitura-do-grafo→gravação contra escritores
    /// concorrentes: sem isso, duas arestas distintas e mutuamente consistentes
    /// isoladamente (ex. A→B e, em paralelo, B→A) podem passar a guarda de ciclo
    /// cada uma vendo o grafo sem a aresta da outra, e juntas formarem um ciclo
    /// que nenhuma UNIQUE de par protege. Deve ser chamado <b>antes</b> de
    /// <see cref="ListarVivasAsync"/>, dentro da mesma transação do handler — o
    /// lock (advisory, escopo de transação) libera sozinho no commit/rollback.
    /// </summary>
    Task TravarGrafoParaEscritaAsync(CancellationToken cancellationToken);

    Task AdicionarAsync(PrecedenciaFase aresta, CancellationToken cancellationToken);

    /// <summary>
    /// Marca a aresta para remoção; o <c>SoftDeleteInterceptor</c> converte em
    /// soft-delete preenchendo <c>DeletedBy</c>/<c>DeletedAt</c>.
    /// </summary>
    void Remover(PrecedenciaFase aresta);
}
