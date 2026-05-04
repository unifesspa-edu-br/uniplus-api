namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

using System.Globalization;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Extensões de <see cref="ControllerBase"/> para responder coleções
/// paginadas com cursor (ADR-0026): encoda o cursor da próxima página,
/// monta o header <c>Link</c> (RFC 5988/8288) e o header <c>X-Page-Size</c>,
/// e devolve o body como array JSON puro (ADR-0025).
/// </summary>
public static class PaginationControllerExtensions
{
    /// <summary>
    /// Devolve <see cref="StatusCodes.Status200OK"/> com os <paramref name="items"/>
    /// como array JSON; encoda o cursor da próxima página quando
    /// <paramref name="nextAfterId"/> não é <c>null</c>; popula
    /// <c>Link</c> e <c>X-Page-Size</c>.
    /// </summary>
    public static async Task<IActionResult> OkPaginatedAsync<T>(
        this ControllerBase controller,
        IReadOnlyList<T> items,
        Guid? nextAfterId,
        PageRequest page,
        string resource,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(page);
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);

        IServiceProvider services = controller.HttpContext.RequestServices;
        CursorEncoder encoder = services.GetRequiredService<CursorEncoder>();
        CursorPaginationOptions options = services.GetRequiredService<IOptions<CursorPaginationOptions>>().Value;
        TimeProvider timeProvider = services.GetRequiredService<TimeProvider>();

        string? nextCursor = null;
        if (nextAfterId is { } proximo)
        {
            CursorPayload payload = new(
                After: proximo.ToString(),
                Limit: page.Limit,
                ResourceTag: resource,
                ExpiresAt: timeProvider.GetUtcNow().Add(options.CursorTtl));
            nextCursor = await encoder.EncodeAsync(payload, cancellationToken).ConfigureAwait(false);
        }

        HttpRequest request = controller.Request;
        string? originalCursor = request.Query["cursor"];
        string? originalLimit = request.Query["limit"];
        int? originalLimitParsed = int.TryParse(originalLimit, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : null;

        PageLinks links = new(
            Self: BuildLink(request, originalCursor, originalLimitParsed),
            Next: nextCursor is null ? null : BuildLink(request, nextCursor, page.Limit),
            Prev: null);

        controller.Response.Headers["Link"] = LinkHeaderBuilder.Build(links);
        controller.Response.Headers["X-Page-Size"] = items.Count.ToString(CultureInfo.InvariantCulture);

        return controller.Ok(items);
    }

    private static string BuildLink(HttpRequest request, string? cursor, int? limit)
    {
        string baseUrl = $"{request.Scheme}://{request.Host}{request.Path}";
        List<string> parts = [];
        if (!string.IsNullOrEmpty(cursor))
            parts.Add($"cursor={Uri.EscapeDataString(cursor)}");
        if (limit is { } l)
            parts.Add($"limit={l.ToString(CultureInfo.InvariantCulture)}");
        return parts.Count == 0 ? baseUrl : $"{baseUrl}?{string.Join('&', parts)}";
    }
}
