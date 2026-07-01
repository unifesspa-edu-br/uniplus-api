namespace Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Repositório da entidade <see cref="Modalidade"/> (ADR-0054: banco isolado
/// <c>uniplus_configuracao</c>). Todas as leituras excluem registros soft-deleted
/// via query filter por convenção.
/// </summary>
public interface IModalidadeRepository
{
    /// <summary>Carrega a modalidade rastreada pelo contexto, para mutação.</summary>
    Task<Modalidade?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Carrega a modalidade para leitura (<c>AsNoTracking</c>) — projeção em DTO.</summary>
    Task<Modalidade?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Lista modalidades vivas paginadas por cursor keyset bidirecional (ADR-0026 +
    /// ADR-0089): ordena por <c>Id</c> (Guid v7, ADR-0032) e devolve as âncoras de
    /// <c>prev</c>/<c>next</c> (nulas quando não há aquele lado).
    /// </summary>
    Task<(IReadOnlyList<Modalidade> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken);

    Task AdicionarAsync(Modalidade modalidade, CancellationToken cancellationToken);

    /// <summary>
    /// Marca a modalidade para remoção; o <c>SoftDeleteInterceptor</c> converte em
    /// soft-delete preenchendo <c>DeletedBy</c>/<c>DeletedAt</c>.
    /// </summary>
    void Remover(Modalidade modalidade);

    /// <summary>
    /// Verifica se existe modalidade viva com o <paramref name="codigo"/>
    /// (case-sensitive, sobre o valor normalizado por <c>Trim</c>), excluindo
    /// opcionalmente um <paramref name="excluirId"/> (para a checagem na atualização).
    /// </summary>
    Task<bool> CodigoExisteEntreVivosAsync(
        string codigo,
        Guid? excluirId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Indica se <b>todos</b> os <paramref name="codigos"/> informados correspondem
    /// a modalidades vivas — integridade referencial dos códigos citados em
    /// <c>ComposicaoOrigem</c> e <c>RemanejamentoArgs</c>. Coleção vazia retorna
    /// <see langword="true"/> (nada a validar).
    /// </summary>
    Task<bool> CodigosVivosExistemAsync(
        IReadOnlyCollection<string> codigos,
        CancellationToken cancellationToken);

    /// <summary>
    /// Indica se ESTE <paramref name="codigo"/> é referenciado por <b>outra</b>
    /// modalidade viva (excluindo <paramref name="excluirId"/>) — como
    /// <c>composicao_origem</c> OU como destino/par/fallback em
    /// <c>remanejamento_args</c> (consulta jsonb). Base do bloqueio de remoção.
    /// </summary>
    Task<bool> EhReferenciadaPorOutraModalidadeVivaAsync(
        string codigo,
        Guid? excluirId,
        CancellationToken cancellationToken);
}
