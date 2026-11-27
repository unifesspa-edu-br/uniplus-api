namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Aplica paginação keyset bidirecional (ADR-0089) sobre uma query <b>já
/// filtrada</b> (os filtros de negócio, ex.: <c>q</c>/<c>tipo</c>, devem estar
/// aplicados em <c>filtered</c> antes da chamada). Chave de
/// ordenação = <c>Id</c> (GUID v7 ordenável, ADR-0032).
/// </summary>
/// <remarks>
/// <para><b>Forward</b> (<see cref="PaginationDirection.Next"/>): <c>Id &gt;
/// ponto de continuação</c>, <c>ORDER BY Id ASC</c>.</para>
/// <para><b>Backward</b> (<see cref="PaginationDirection.Prev"/>): <c>Id &lt;
/// ponto de continuação</c>, <c>ORDER BY Id DESC</c>; o item-probe é cortado <b>ainda em
/// DESC</b> (é o mais antigo) e só então a lista é revertida para ascendente —
/// reverter antes de cortar omitiria o boundary.</para>
/// <para><b>Flags exatas sem <c>COUNT</c></b>: o lado navegado vem do probe
/// <c>n+1</c>; o lado oposto vem de um <c>EXISTS</c> indexado sobre a mesma
/// query base (mesmos filtros).</para>
/// <para><b>Condições obrigatórias:</b> a chave do ponto de continuação leva o resumo das
/// <see cref="CondicoesDoFiltro"/>, conferido antes de qualquer consulta; esquecê-las deixa de compilar.</para>
/// </remarks>
public static class CursorKeyset
{
    public static async Task<OrderedKeysetPage<T>> ApplyAsync<T>(
        IQueryable<T> filtered,
        CondicoesDoFiltro conditions,
        string? afterSortKey,
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken = default)
        where T : IIdentificavel
    {
        ArgumentNullException.ThrowIfNull(filtered);
        ArgumentNullException.ThrowIfNull(conditions);
        if (afterId is not null)
            conditions.ConferirChave(afterSortKey);

        PaginaPorId<T> page = direction == PaginationDirection.Prev
            ? await BackwardAsync(filtered, afterId, limit, cancellationToken).ConfigureAwait(false)
            : await ForwardAsync(filtered, afterId, limit, cancellationToken).ConfigureAwait(false);

        string key = conditions.EmitirChave();
        return new OrderedKeysetPage<T>(
            page.Items,
            page.Previous is { } previous ? (key, previous) : null,
            page.Next is { } next ? (key, next) : null);
    }

    private static async Task<PaginaPorId<T>> ForwardAsync<T>(
        IQueryable<T> filtered, Guid? afterId, int limit, CancellationToken cancellationToken)
        where T : IIdentificavel
    {
        IQueryable<T> seek = afterId is { } point ? filtered.Where(e => e.Id.CompareTo(point) > 0) : filtered;
        List<T> items = await seek.OrderBy(e => e.Id).Take(limit + 1).ToListAsync(cancellationToken).ConfigureAwait(false);
        bool hasNext = TrimProbe(items, limit);
        if (items.Count == 0)
            return new PaginaPorId<T>(items, Previous: null, Next: null);

        Guid first = items[0].Id;
        bool hasPrevious = afterId is not null && await ExistsBeforeAsync(filtered, first, cancellationToken).ConfigureAwait(false);
        return new PaginaPorId<T>(items, hasPrevious ? first : null, hasNext ? items[^1].Id : null);
    }

    private static async Task<PaginaPorId<T>> BackwardAsync<T>(
        IQueryable<T> filtered, Guid? afterId, int limit, CancellationToken cancellationToken)
        where T : IIdentificavel
    {
        IQueryable<T> seek = afterId is { } point ? filtered.Where(e => e.Id.CompareTo(point) < 0) : filtered;
        List<T> items = await seek.OrderByDescending(e => e.Id).Take(limit + 1).ToListAsync(cancellationToken).ConfigureAwait(false);
        bool hasPrevious = TrimProbe(items, limit);
        items.Reverse();
        if (items.Count == 0)
            return new PaginaPorId<T>(items, Previous: null, Next: null);

        Guid last = items[^1].Id;
        bool hasNext = await ExistsAfterAsync(filtered, last, cancellationToken).ConfigureAwait(false);
        return new PaginaPorId<T>(items, hasPrevious ? items[0].Id : null, hasNext ? last : null);
    }

    // O item a mais, lido só para saber se há outro lado, sai antes de a página ser invertida.
    private static bool TrimProbe<T>(List<T> items, int limit)
    {
        bool hasProbe = items.Count > limit;
        if (hasProbe)
            items.RemoveAt(items.Count - 1);

        return hasProbe;
    }

    private static Task<bool> ExistsBeforeAsync<T>(IQueryable<T> filtered, Guid id, CancellationToken cancellationToken)
        where T : IIdentificavel =>
        filtered.AnyAsync(e => e.Id.CompareTo(id) < 0, cancellationToken);

    private static Task<bool> ExistsAfterAsync<T>(IQueryable<T> filtered, Guid id, CancellationToken cancellationToken)
        where T : IIdentificavel =>
        filtered.AnyAsync(e => e.Id.CompareTo(id) > 0, cancellationToken);

    private sealed record PaginaPorId<T>(IReadOnlyList<T> Items, Guid? Previous, Guid? Next);
}
