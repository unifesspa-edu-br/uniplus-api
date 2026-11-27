namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

using Microsoft.EntityFrameworkCore;

using MR.EntityFrameworkCore.KeysetPagination;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Página keyset (ADR-0094): <see cref="Items"/> em ordem ascendente
/// canônica pela chave de ordenação + <c>Id</c>, e os pontos de continuação
/// <c>(SortKey, Id)</c> para emitir <c>rel="prev"</c>/<c>rel="next"</c>. Ponto
/// nulo = não há aquele lado.
/// </summary>
public sealed record OrderedKeysetPage<T>(
    IReadOnlyList<T> Items,
    (string SortKey, Guid Id)? Previous,
    (string SortKey, Guid Id)? Next);

/// <summary>
/// Aplica paginação keyset multi-coluna ordenada (ADR-0094) sobre uma query <b>já
/// filtrada</b>, usando a biblioteca de seek da <c>MR.EntityFrameworkCore.KeysetPagination</c>
/// sob a nossa camada de cursor opaco (ADR-0026). A chave de ordenação é definida pelo
/// chamador por meio de um <see cref="KeysetSort{T}"/>, que também fornece como extrair a chave de um item e
/// como montar o ponto de continuação — assim o <c>Id</c>-only (<see cref="CursorKeyset"/>) segue à parte para
/// quem não ordena.
/// </summary>
/// <remarks>
/// <para><b>Chave não-nula (ADR-0095):</b> a MR não suporta coluna nullable no keyset
/// (NULL invalida o <c>WHERE</c> do seek → zero resultados). O chamador deve coalescer a
/// sort key para não-nulo antes de passá-la ao builder; o índice correspondente deve casar
/// a mesma expressão/coluna de ordenação.</para>
/// <para><b>Flags bidirecionais (ADR-0089):</b> <c>EnsureCorrectOrder</c> restaura a ordem
/// ascendente na navegação <c>Backward</c>; <c>HasPreviousAsync</c>/<c>HasNextAsync</c>
/// resolvem os lados por <c>EXISTS</c> indexado (sem <c>COUNT</c>).</para>
/// <para><b>Ciclo de vida da entidade é irrelevante aqui:</b> a restrição é
/// <see cref="IIdentificavel"/>, a base comum de <c>EntityBase</c> e de
/// <c>IForensicEntity</c> — o keyset só precisa de um <c>Id</c> ordenável para
/// desempatar a chave de ordenação. Assim uma entidade append-only (ex.: o ato
/// publicado) pagina do mesmo modo, sem herdar soft-delete nem auditoria.</para>
/// </remarks>
public static class OrderedKeysetCursor
{
    /// <summary>
    /// Pagina segundo uma <see cref="KeysetSort{T}"/> — uma ou mais colunas,
    /// cada uma com o próprio sentido, e o <c>Id</c> como desempate final. A
    /// chave do ponto de continuação leva o resumo da consulta e as colunas, e viaja inteira no cursor.
    /// </summary>
    /// <exception cref="CursorAnchorMismatchException">
    /// A chave não corresponde à consulta pedida (ordenação, filtro, busca ou recorte), antes de qualquer leitura.
    /// </exception>
    public static async Task<OrderedKeysetPage<T>> ApplyAsync<T>(
        IQueryable<T> filtered,
        KeysetSort<T> sort,
        string? afterSortKey,
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken = default)
        where T : class, IIdentificavel
    {
        ArgumentNullException.ThrowIfNull(filtered);
        ArgumentNullException.ThrowIfNull(sort);

        KeysetPaginationContext<T> context = filtered.KeysetPaginate(
            sort.ConfigureKeyset, DirectionOf(direction), ReferenceFor(sort, afterSortKey, afterId));
        List<T> items = await context.Query.Take(limit).ToListAsync(cancellationToken).ConfigureAwait(false);
        context.EnsureCorrectOrder(items);

        return items.Count == 0
            ? new OrderedKeysetPage<T>(items, Previous: null, Next: null)
            : await PageOfAsync(context, sort, items).ConfigureAwait(false);
    }

    // Flags exatas por EXISTS indexado (sem COUNT) — ADR-0089.
    private static async Task<OrderedKeysetPage<T>> PageOfAsync<T>(
        KeysetPaginationContext<T> context, KeysetSort<T> sort, List<T> items)
        where T : class, IIdentificavel
    {
        bool hasPrevious = await context.HasPreviousAsync(items).ConfigureAwait(false);
        bool hasNext = await context.HasNextAsync(items).ConfigureAwait(false);
        T first = items[0];
        T last = items[^1];

        return new OrderedKeysetPage<T>(
            items,
            Previous: hasPrevious ? (sort.ContinuationKey(first), first.Id) : null,
            Next: hasNext ? (sort.ContinuationKey(last), last.Id) : null);
    }

    private static KeysetPaginationDirection DirectionOf(PaginationDirection direction) =>
        direction == PaginationDirection.Prev ? KeysetPaginationDirection.Backward : KeysetPaginationDirection.Forward;

    // Ponto completo (chave + Id) ⇒ continuação; ausente ⇒ primeira página.
    private static object? ReferenceFor<T>(KeysetSort<T> sort, string? afterSortKey, Guid? afterId)
        where T : class, IIdentificavel =>
        afterId is { } id
            ? sort.TryBuildContinuationPoint(afterSortKey, id, out object point)
                ? point
                : throw new CursorAnchorMismatchException()
            : null;
}
