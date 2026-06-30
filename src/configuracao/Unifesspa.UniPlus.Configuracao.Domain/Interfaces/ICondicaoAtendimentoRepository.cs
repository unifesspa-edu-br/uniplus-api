namespace Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Repositório da entidade <see cref="CondicaoAtendimentoEspecializado"/>
/// (ADR-0054: banco isolado <c>uniplus_configuracao</c>). Todas as leituras
/// excluem registros soft-deleted via query filter por convenção.
/// </summary>
public interface ICondicaoAtendimentoRepository
{
    /// <summary>Carrega a condição rastreada pelo contexto, para mutação.</summary>
    Task<CondicaoAtendimentoEspecializado?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Carrega a condição para leitura (<c>AsNoTracking</c>) — projeção em DTO.</summary>
    Task<CondicaoAtendimentoEspecializado?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Lista condições vivas paginadas por cursor keyset bidirecional (ADR-0026 +
    /// ADR-0089): ordena por <c>Id</c> (Guid v7, ADR-0032) e devolve as âncoras de
    /// <c>prev</c>/<c>next</c> (nulas quando não há aquele lado).
    /// </summary>
    Task<(IReadOnlyList<CondicaoAtendimentoEspecializado> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken);

    Task AdicionarAsync(CondicaoAtendimentoEspecializado condicao, CancellationToken cancellationToken);

    /// <summary>
    /// Marca a condição para remoção; o <c>SoftDeleteInterceptor</c> converte em
    /// soft-delete preenchendo <c>DeletedBy</c>/<c>DeletedAt</c>.
    /// </summary>
    void Remover(CondicaoAtendimentoEspecializado condicao);

    /// <summary>
    /// Verifica se existe condição viva com o <paramref name="codigo"/>
    /// (case-sensitive, sobre o valor normalizado por <c>Trim</c>), excluindo
    /// opcionalmente um <paramref name="excluirId"/> (para a checagem na atualização).
    /// </summary>
    Task<bool> CodigoExisteEntreVivosAsync(
        string codigo,
        Guid? excluirId,
        CancellationToken cancellationToken);
}
