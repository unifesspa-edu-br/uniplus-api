namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

using System.Globalization;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

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
        bool requireUserBinding = false,
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

        // User-binding (ADR-0026 §"User-binding em cursores user-scoped"):
        // recurso user-scoped popula UserId no payload com o sub do principal
        // corrente — emissão e decode validam o mesmo binding. Resolução de
        // IUserContext só acontece quando há próxima página A SER EMITIDA;
        // last-page (sem cursor) não toca o DI evitando friction em testes
        // slice-level que registram só CursorEncoder/TimeProvider.
        string? nextCursor = null;
        if (nextAfterId is { } proximo)
        {
            string? userId = null;
            if (requireUserBinding)
            {
                IUserContext userContext = services.GetRequiredService<IUserContext>();
                if (!userContext.IsAuthenticated || string.IsNullOrEmpty(userContext.UserId))
                {
                    throw new InvalidOperationException(
                        "OkPaginatedAsync com requireUserBinding=true exige principal autenticado. " +
                        "Verifique se o endpoint tem [Authorize] e se o middleware de auth está antes do MVC.");
                }
                userId = userContext.UserId;
            }

            CursorPayload payload = new(
                After: proximo.ToString(),
                Limit: page.Limit,
                ResourceTag: resource,
                ExpiresAt: timeProvider.GetUtcNow().Add(options.CursorTtl),
                UserId: userId);
            nextCursor = await encoder.EncodeAsync(payload, cancellationToken).ConfigureAwait(false);
        }

        HttpRequest request = controller.Request;
        string? originalCursor = request.Query["cursor"];
        string? originalLimit = request.Query["limit"];
        int? originalLimitParsed = int.TryParse(originalLimit, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : null;

        // Self espelha o request original (cursor + limit como vieram).
        // Next NÃO inclui limit: o cursor já carrega o tamanho de janela
        // (clampado server-side no decode). Incluir ?limit=N na URL faria
        // o validator do binder rodar primeiro e rejeitar com 422 caso a
        // config tenha apertado LimitMax após a emissão do cursor —
        // quebrando navegação de cursores stale que ainda estariam OK.
        PageLinks links = new(
            Self: BuildLink(request, originalCursor, originalLimitParsed),
            Next: nextCursor is null ? null : BuildLink(request, nextCursor, limit: null),
            Prev: null);

        controller.Response.Headers["Link"] = LinkHeaderBuilder.Build(links);
        controller.Response.Headers["X-Page-Size"] = items.Count.ToString(CultureInfo.InvariantCulture);

        return controller.Ok(items);
    }

    private static string BuildLink(HttpRequest request, string? cursor, int? limit)
    {
        // Inclui PathBase para honrar deploys atrás de reverse proxy
        // (Traefik, nginx, ingress) ou hosts com app.UsePathBase("/foo").
        // Sem isso, links de navegação apontam para /api/editais em vez
        // de /foo/api/editais e clientes recebem 404 ao seguir Link header.
        string baseUrl = $"{request.Scheme}://{request.Host}{request.PathBase}{request.Path}";
        List<string> parts = [];
        if (!string.IsNullOrEmpty(cursor))
            parts.Add($"cursor={Uri.EscapeDataString(cursor)}");
        if (limit is { } l)
            parts.Add($"limit={l.ToString(CultureInfo.InvariantCulture)}");
        return parts.Count == 0 ? baseUrl : $"{baseUrl}?{string.Join('&', parts)}";
    }
}
