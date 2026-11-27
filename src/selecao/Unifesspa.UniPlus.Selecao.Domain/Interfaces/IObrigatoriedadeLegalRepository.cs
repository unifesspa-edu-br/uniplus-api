namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using System.Collections.Generic;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Filtros admin da listagem de regras. <c>VigentesEm</c>, quando presente, exclui as regras não vigentes nesse dia;
/// o dia não entra no resumo do cursor, só o fato de filtrar. O código do tipo de processo vazio equivale a não filtrar.
/// </summary>
public sealed record FiltroDasObrigatoriedadesLegais(string? TipoProcessoCodigo, CategoriaObrigatoriedade? Categoria, DateOnly? VigentesEm);

/// <summary>
/// Repositório de <see cref="ObrigatoriedadeLegal"/>.
/// </summary>
/// <remarks>
/// Listagem paginada por chave (ADR-0026) com filtros admin
/// (<c>tipoProcessoCodigo</c>, <c>categoria</c>, dia de vigência). Consultas
/// para a conferência (<c>ObterVigentesParaTipoProcessoAsync</c>)
/// carregam regras para todos os tipos (<c>TODOS_OS_TIPOS</c>) + específicas do tipo.
/// </remarks>
public interface IObrigatoriedadeLegalRepository : IRepository<ObrigatoriedadeLegal>
{
    /// <summary>
    /// Lista regras paginadas por cursor keyset bidirecional (ADR-0026 +
    /// ADR-0089) com filtros admin. Ordenação estável por <c>Id</c> (Guid v7
    /// cronológico); retorna até <paramref name="limit"/> itens na direção
    /// <paramref name="direction"/> a partir de <paramref name="afterId"/>,
    /// sempre em ordem ascendente, com os pontos de continuação de <c>prev</c>/<c>next</c>.
    /// Com <c>VigentesEm</c> no filtro, só as regras vigentes nesse dia civil (início inclusivo, fim exclusivo).
    /// </summary>
    Task<(IReadOnlyList<ObrigatoriedadeLegal> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)> ListarPaginadoAsync(
        string? afterSortKey,
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        FiltroDasObrigatoriedadesLegais filtro,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lê o conjunto de regras vigentes aplicáveis ao tipo de processo
    /// (filtra <c>TipoProcessoCodigo = TODOS_OS_TIPOS OR tipo</c>, vigência ativa,
    /// não soft-deleted). Caminho da conferência.
    /// </summary>
    Task<IReadOnlyList<ObrigatoriedadeLegal>> ObterVigentesParaTipoProcessoAsync(
        string tipoProcessoCodigo,
        DateOnly dataReferencia,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica colisão de <c>RegraCodigo</c> ativo (não soft-deleted).
    /// Usado para emitir <c>409 Conflict</c> com
    /// <c>uniplus.selecao.obrigatoriedade_legal.regra_codigo_duplicada</c>
    /// antes de tentar a INSERT que dispararia a <c>UNIQUE</c> parcial.
    /// </summary>
    Task<bool> ExisteRegraCodigoAtivoAsync(
        string regraCodigo,
        Guid? excluirId,
        CancellationToken cancellationToken = default);
}
