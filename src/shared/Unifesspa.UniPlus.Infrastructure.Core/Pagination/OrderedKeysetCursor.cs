namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

using Microsoft.EntityFrameworkCore;

using MR.EntityFrameworkCore.KeysetPagination;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Página keyset ordenada (ADR-0094): <see cref="Items"/> em ordem ascendente
/// canônica pela chave de ordenação + <c>Id</c>, e as âncoras
/// <c>(SortKey, Id)</c> para emitir <c>rel="prev"</c>/<c>rel="next"</c>. Âncora
/// nula = não há aquele lado.
/// </summary>
public sealed record OrderedKeysetPage<T>(
    IReadOnlyList<T> Items,
    (string SortKey, Guid Id)? Previous,
    (string SortKey, Guid Id)? Next);

/// <summary>
/// Aplica paginação keyset multi-coluna ordenada (ADR-0094) sobre uma query <b>já
/// filtrada</b>, usando o motor de seek da <c>MR.EntityFrameworkCore.KeysetPagination</c>
/// sob a nossa camada de cursor opaco (ADR-0026). A chave de ordenação é definida pelo
/// chamador (ex.: <c>b =&gt; b.Ascending(x =&gt; x.NomeOrdenacao).Ascending(x =&gt; x.Id)</c>),
/// que também fornece como extrair a sort key de um item e como montar a âncora — assim o
/// <c>Id</c>-only (<see cref="CursorKeyset"/>) segue intocado para quem não ordena.
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
/// publicado) pagina pelo mesmo motor, sem herdar soft-delete nem auditoria.</para>
/// </remarks>
public static class OrderedKeysetCursor
{
    /// <summary>
    /// Pagina segundo uma <see cref="KeysetSort{T}"/> — uma ou mais colunas,
    /// cada uma com o próprio sentido, e o <c>Id</c> como desempate final. A
    /// chave de ordenação da âncora é composta pelas colunas e viaja inteira no
    /// cursor.
    /// </summary>
    /// <exception cref="CursorAnchorMismatchException">
    /// A chave de ordenação não corresponde à ordenação pedida.
    /// </exception>
    public static Task<OrderedKeysetPage<T>> ApplyAsync<T>(
        IQueryable<T> filtered,
        KeysetSort<T> sort,
        string? afterSortKey,
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken = default)
        where T : class, IIdentificavel
    {
        ArgumentNullException.ThrowIfNull(sort);

        return ApplyAsync(
            filtered,
            sort.ConfigureKeyset,
            sort.AnchorKey,
            (key, id) => sort.TryBuildAnchor(key, id, out object anchor)
                ? anchor
                : throw new CursorAnchorMismatchException(),
            afterSortKey,
            afterId,
            limit,
            direction,
            cancellationToken);
    }

    public static async Task<OrderedKeysetPage<T>> ApplyAsync<T>(
        IQueryable<T> filtered,
        Action<KeysetPaginationBuilder<T>> buildKeyset,
        Func<T, string> anchorKeyOf,
        Func<string, Guid, object> buildAnchor,
        string? afterSortKey,
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken = default)
        where T : class, IIdentificavel
    {
        ArgumentNullException.ThrowIfNull(filtered);
        ArgumentNullException.ThrowIfNull(buildKeyset);
        ArgumentNullException.ThrowIfNull(anchorKeyOf);
        ArgumentNullException.ThrowIfNull(buildAnchor);

        KeysetPaginationDirection mrDirection = direction == PaginationDirection.Prev
            ? KeysetPaginationDirection.Backward
            : KeysetPaginationDirection.Forward;

        // Âncora completa (sort key + Id) ⇒ continuação; ausente ⇒ primeira página.
        object? reference = afterSortKey is not null && afterId is not null
            ? buildAnchor(afterSortKey, afterId.Value)
            : null;

        KeysetPaginationContext<T> context = filtered.KeysetPaginate(buildKeyset, mrDirection, reference);

        List<T> items = await context.Query
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Backward devolve em ordem inversa; restaura a ordem ascendente canônica.
        context.EnsureCorrectOrder(items);

        if (items.Count == 0)
        {
            return new OrderedKeysetPage<T>(items, Previous: null, Next: null);
        }

        // Flags exatas por EXISTS indexado (sem COUNT) — ADR-0089.
        bool hasPrevious = await context.HasPreviousAsync(items).ConfigureAwait(false);
        bool hasNext = await context.HasNextAsync(items).ConfigureAwait(false);

        T first = items[0];
        T last = items[^1];

        return new OrderedKeysetPage<T>(
            items,
            Previous: hasPrevious ? (anchorKeyOf(first), first.Id) : null,
            Next: hasNext ? (anchorKeyOf(last), last.Id) : null);
    }
}
