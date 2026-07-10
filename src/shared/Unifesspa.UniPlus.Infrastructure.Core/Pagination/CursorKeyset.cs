namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Página keyset bidirecional (ADR-0089): <see cref="Items"/> já em ordem
/// ascendente canônica por <c>Id</c> e as âncoras para emissão de
/// <c>rel="prev"</c>/<c>rel="next"</c>. Âncora nula = não há aquele lado.
/// </summary>
public sealed record CursorKeysetPage<T>(
    IReadOnlyList<T> Items,
    Guid? PrevAfterId,
    Guid? NextAfterId)
{
    /// <summary>Há página anterior (emite <c>rel="prev"</c>).</summary>
    public bool HasPrevious => PrevAfterId is not null;

    /// <summary>Há próxima página (emite <c>rel="next"</c>).</summary>
    public bool HasNext => NextAfterId is not null;
}

/// <summary>
/// Aplica paginação keyset bidirecional (ADR-0089) sobre uma query <b>já
/// filtrada</b> (os filtros de negócio, ex.: <c>q</c>/<c>tipo</c>, devem estar
/// aplicados em <paramref name="filtered"/> antes da chamada). Chave de
/// ordenação = <c>Id</c> (GUID v7 ordenável, ADR-0032).
/// </summary>
/// <remarks>
/// <para><b>Forward</b> (<see cref="PaginationDirection.Next"/>): <c>Id &gt;
/// âncora</c>, <c>ORDER BY Id ASC</c>.</para>
/// <para><b>Backward</b> (<see cref="PaginationDirection.Prev"/>): <c>Id &lt;
/// âncora</c>, <c>ORDER BY Id DESC</c>; o item-probe é cortado <b>ainda em
/// DESC</b> (é o mais antigo) e só então a lista é revertida para ascendente —
/// reverter antes de cortar omitiria o boundary.</para>
/// <para><b>Flags exatas sem <c>COUNT</c></b>: o lado navegado vem do probe
/// <c>n+1</c>; o lado oposto vem de um <c>EXISTS</c> indexado sobre a mesma
/// query base (mesmos filtros).</para>
/// </remarks>
public static class CursorKeyset
{
    public static async Task<CursorKeysetPage<T>> ApplyAsync<T>(
        IQueryable<T> filtered,
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken = default)
        where T : IIdentificavel
    {
        ArgumentNullException.ThrowIfNull(filtered);

        return direction == PaginationDirection.Prev
            ? await ApplyBackwardAsync(filtered, afterId, limit, cancellationToken).ConfigureAwait(false)
            : await ApplyForwardAsync(filtered, afterId, limit, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<CursorKeysetPage<T>> ApplyForwardAsync<T>(
        IQueryable<T> filtered,
        Guid? afterId,
        int limit,
        CancellationToken cancellationToken)
        where T : IIdentificavel
    {
        IQueryable<T> forward = afterId is { } anchor
            ? filtered.Where(e => e.Id.CompareTo(anchor) > 0)
            : filtered;

        List<T> fetched = await forward
            .OrderBy(e => e.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        bool hasNext = fetched.Count > limit;
        if (hasNext)
            fetched.RemoveAt(fetched.Count - 1);

        if (fetched.Count == 0)
            return new CursorKeysetPage<T>(fetched, PrevAfterId: null, NextAfterId: null);

        // hasPrevious só faz sentido quando há âncora (não é a primeira página);
        // confirmado por EXISTS indexado (sem COUNT) sobre a query filtrada.
        // Boundaries extraídos para locais — expression tree não aceita índice.
        Guid firstId = fetched[0].Id;
        Guid lastId = fetched[^1].Id;
        bool hasPrevious = afterId is not null
            && await filtered
                .AnyAsync(e => e.Id.CompareTo(firstId) < 0, cancellationToken)
                .ConfigureAwait(false);

        return new CursorKeysetPage<T>(
            fetched,
            PrevAfterId: hasPrevious ? firstId : null,
            NextAfterId: hasNext ? lastId : null);
    }

    private static async Task<CursorKeysetPage<T>> ApplyBackwardAsync<T>(
        IQueryable<T> filtered,
        Guid? afterId,
        int limit,
        CancellationToken cancellationToken)
        where T : IIdentificavel
    {
        IQueryable<T> backward = afterId is { } anchor
            ? filtered.Where(e => e.Id.CompareTo(anchor) < 0)
            : filtered;

        List<T> fetchedDesc = await backward
            .OrderByDescending(e => e.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        bool hasPrevious = fetchedDesc.Count > limit;
        // Corta o item-probe AINDA em DESC (é o mais antigo) e só depois reverte.
        if (hasPrevious)
            fetchedDesc.RemoveAt(fetchedDesc.Count - 1);
        fetchedDesc.Reverse();
        List<T> page = fetchedDesc;

        if (page.Count == 0)
            return new CursorKeysetPage<T>(page, PrevAfterId: null, NextAfterId: null);

        // Boundaries em locais — expression tree não aceita índice from-end.
        Guid firstId = page[0].Id;
        Guid lastId = page[^1].Id;
        bool hasNext = await filtered
            .AnyAsync(e => e.Id.CompareTo(lastId) > 0, cancellationToken)
            .ConfigureAwait(false);

        return new CursorKeysetPage<T>(
            page,
            PrevAfterId: hasPrevious ? firstId : null,
            NextAfterId: hasNext ? lastId : null);
    }
}
