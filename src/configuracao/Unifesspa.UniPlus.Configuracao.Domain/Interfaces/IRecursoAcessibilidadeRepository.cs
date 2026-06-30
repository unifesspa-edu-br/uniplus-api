namespace Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Repositório da entidade <see cref="RecursoAcessibilidade"/> (ADR-0054: banco
/// isolado <c>uniplus_configuracao</c>). Todas as leituras excluem registros
/// soft-deleted via query filter por convenção.
/// </summary>
public interface IRecursoAcessibilidadeRepository
{
    /// <summary>Carrega o recurso de acessibilidade rastreado pelo contexto, para mutação.</summary>
    Task<RecursoAcessibilidade?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Carrega o recurso de acessibilidade para leitura (<c>AsNoTracking</c>) — projeção em DTO.</summary>
    Task<RecursoAcessibilidade?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Lista recursos de acessibilidade vivos paginados por cursor keyset
    /// bidirecional (ADR-0026 + ADR-0089): ordena por <c>Id</c> (Guid v7, ADR-0032)
    /// e devolve as âncoras de <c>prev</c>/<c>next</c> (nulas quando não há aquele lado).
    /// </summary>
    Task<(IReadOnlyList<RecursoAcessibilidade> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken);

    Task AdicionarAsync(RecursoAcessibilidade recurso, CancellationToken cancellationToken);

    /// <summary>
    /// Marca o recurso de acessibilidade para remoção; o <c>SoftDeleteInterceptor</c>
    /// converte em soft-delete preenchendo <c>DeletedBy</c>/<c>DeletedAt</c>.
    /// </summary>
    void Remover(RecursoAcessibilidade recurso);

    /// <summary>
    /// Verifica se existe recurso de acessibilidade vivo com o <paramref name="nome"/>
    /// (case-sensitive, sobre o valor normalizado por <c>Trim</c>), excluindo
    /// opcionalmente um <paramref name="excluirId"/> (para a checagem na atualização).
    /// </summary>
    Task<bool> NomeExisteEntreVivosAsync(
        string nome,
        Guid? excluirId,
        CancellationToken cancellationToken);
}
