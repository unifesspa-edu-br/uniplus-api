namespace Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Repositório da entidade <see cref="Curso"/> (ADR-0054: banco isolado
/// <c>uniplus_configuracao</c>). Todas as leituras excluem registros
/// soft-deleted via query filter por convenção.
/// </summary>
public interface ICursoRepository
{
    /// <summary>Carrega o curso rastreado pelo contexto, para mutação.</summary>
    Task<Curso?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Carrega o curso para leitura (<c>AsNoTracking</c>) — projeção em DTO.</summary>
    Task<Curso?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Lista cursos vivos paginados por cursor keyset bidirecional
    /// (ADR-0026 + ADR-0089): ordena por <c>Id</c> (Guid v7, ADR-0032) e devolve
    /// as âncoras de <c>prev</c>/<c>next</c> (nulas quando não há aquele lado).
    /// </summary>
    Task<(IReadOnlyList<Curso> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken);

    Task AdicionarAsync(Curso curso, CancellationToken cancellationToken);

    /// <summary>
    /// Marca o curso para remoção; o <c>SoftDeleteInterceptor</c> converte em
    /// soft-delete preenchendo <c>DeletedBy</c>/<c>DeletedAt</c>.
    /// </summary>
    void Remover(Curso curso);

    /// <summary>
    /// Verifica se existe curso vivo com o <paramref name="codigo"/>
    /// (case-sensitive, sobre o valor normalizado por <c>Trim</c>), excluindo
    /// opcionalmente um <paramref name="excluirId"/> (para a checagem na atualização).
    /// </summary>
    Task<bool> CodigoExisteEntreVivosAsync(
        string codigo,
        Guid? excluirId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Indica se o curso é referenciado por alguma oferta de curso viva (#749)
    /// — caso em que a remoção é bloqueada. Oferta soft-deletada não conta: o
    /// curso fica livre para remoção assim que a última oferta viva que o
    /// referencia deixa de existir.
    /// </summary>
    Task<bool> ReferenciadoPorOfertaCursoVivaAsync(Guid cursoId, CancellationToken cancellationToken);
}
