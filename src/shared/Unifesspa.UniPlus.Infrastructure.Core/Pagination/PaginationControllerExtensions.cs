namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

using System.Globalization;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Kernel.Pagination;

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
    /// <c>Link</c> e <c>X-Page-Size</c>. Variante keyset por <c>Id</c> (a maioria dos
    /// recursos) — a âncora do cursor é só o <c>Id</c>.
    /// </summary>
    public static Task<IActionResult> OkPaginatedAsync<T>(
        this ControllerBase controller,
        IReadOnlyList<T> items,
        Guid? prevAfterId,
        Guid? nextAfterId,
        PageRequest page,
        string resource,
        bool requireUserBinding = false,
        CancellationToken cancellationToken = default)
    {
        CursorAncora? prev = prevAfterId is { } anterior ? new CursorAncora(anterior.ToString(), SortKey: null) : null;
        CursorAncora? next = nextAfterId is { } proximo ? new CursorAncora(proximo.ToString(), SortKey: null) : null;
        return OkPaginatedCoreAsync(controller, items, prev, next, page, resource, requireUserBinding, cancellationToken);
    }

    /// <summary>
    /// Variante para keyset multi-coluna ordenado (ADR-0094): cada âncora carrega a
    /// chave de ordenação <strong>e</strong> o <c>Id</c> de desempate, serializados no
    /// payload opaco. A camada de cursor (AES-GCM, TTL, user-binding, Link) é a mesma.
    /// </summary>
    public static Task<IActionResult> OkPaginatedOrdenadoAsync<T>(
        this ControllerBase controller,
        IReadOnlyList<T> items,
        (string SortKey, Guid Id)? prev,
        (string SortKey, Guid Id)? next,
        PageRequest page,
        string resource,
        bool requireUserBinding = false,
        CancellationToken cancellationToken = default)
    {
        CursorAncora? prevAncora = prev is { } p ? new CursorAncora(p.Id.ToString(), p.SortKey) : null;
        CursorAncora? nextAncora = next is { } n ? new CursorAncora(n.Id.ToString(), n.SortKey) : null;
        return OkPaginatedCoreAsync(controller, items, prevAncora, nextAncora, page, resource, requireUserBinding, cancellationToken);
    }

    // Âncora de continuação serializada no cursor: After = Id (sempre), SortKey
    // presente apenas no keyset ordenado (ADR-0094).
    private readonly record struct CursorAncora(string After, string? SortKey);

    private static async Task<IActionResult> OkPaginatedCoreAsync<T>(
        ControllerBase controller,
        IReadOnlyList<T> items,
        CursorAncora? prev,
        CursorAncora? next,
        PageRequest page,
        string resource,
        bool requireUserBinding,
        CancellationToken cancellationToken)
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
        // recurso user-scoped vincula o sub do principal corrente ao cursor —
        // emissão e decode validam o mesmo binding. Resolução de IUserContext só
        // acontece quando há ALGUM cursor a emitir (prev ou next); página única
        // sem navegação não toca o DI, evitando friction em testes slice-level
        // que registram só CursorEncoder/TimeProvider.
        string? userId = null;
        if ((prev is not null || next is not null) && requireUserBinding)
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

        DateTimeOffset expiresAt = timeProvider.GetUtcNow().Add(options.CursorTtl);

        // O cursor vincula sua direção (ADR-0089): o link prev cifra a âncora
        // com Direction=Prev; o next, com Direction=Next. O boundary rejeita
        // reuso com a direção trocada. SortKey só viaja no keyset ordenado (ADR-0094).
        string? prevCursor = prev is { } anterior
            ? await encoder.EncodeAsync(
                new CursorPayload(anterior.After, page.Limit, resource, expiresAt, PaginationDirection.Prev, userId, anterior.SortKey),
                cancellationToken).ConfigureAwait(false)
            : null;

        string? nextCursor = next is { } proximo
            ? await encoder.EncodeAsync(
                new CursorPayload(proximo.After, page.Limit, resource, expiresAt, PaginationDirection.Next, userId, proximo.SortKey),
                cancellationToken).ConfigureAwait(false)
            : null;

        HttpRequest request = controller.Request;
        string? originalCursor = request.Query["cursor"];
        string? originalDirection = request.Query["direction"];
        string? originalLimit = request.Query["limit"];
        int? originalLimitParsed = int.TryParse(originalLimit, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : null;

        // Self espelha o request original (cursor + limit + direction como vieram).
        // Prev/Next NÃO incluem limit (o cursor já carrega a janela, clampada no
        // decode) e carregam a direção explícita — a navegação fica auto-suficiente
        // (RFC 5988): o cliente segue o link opaco sem conhecer a convenção.
        PageLinks links = new(
            Self: BuildLink(request, originalCursor, originalLimitParsed, originalDirection),
            Next: nextCursor is null ? null : BuildLink(request, nextCursor, limit: null, "next"),
            Prev: prevCursor is null ? null : BuildLink(request, prevCursor, limit: null, "prev"));

        controller.Response.Headers["Link"] = LinkHeaderBuilder.Build(links);
        controller.Response.Headers["X-Page-Size"] = items.Count.ToString(CultureInfo.InvariantCulture);

        return controller.Ok(items);
    }

    private static readonly string[] ReservedPaginationParams = ["cursor", "limit", "direction"];

    private static string BuildLink(HttpRequest request, string? cursor, int? limit, string? direction)
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
        if (!string.IsNullOrEmpty(direction))
            parts.Add($"direction={Uri.EscapeDataString(direction)}");

        // Preserva os demais query params do request (filtros como q/tipo) nos
        // links self/next. Sem isso, seguir rel="next" numa listagem filtrada
        // voltaria a uma página SEM filtro (RFC 5988: o link deve ser
        // auto-suficiente). Os params de paginação já foram tratados acima e são
        // ignorados aqui; multi-valores (ex.: tipo=3&tipo=4) são preservados.
        IEnumerable<KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues>> filtros =
            request.Query.Where(param =>
                !ReservedPaginationParams.Contains(param.Key, StringComparer.OrdinalIgnoreCase));

        foreach (KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues> param in filtros)
        {
            // OfType<string> descarta os elementos nulos de StringValues e estreita
            // o item para string não-anulável, dispensando o guard imperativo e o
            // operador de supressão de nulidade no Uri.EscapeDataString.
            foreach (string value in param.Value.OfType<string>())
            {
                parts.Add($"{Uri.EscapeDataString(param.Key)}={Uri.EscapeDataString(value)}");
            }
        }

        return parts.Count == 0 ? baseUrl : $"{baseUrl}?{string.Join('&', parts)}";
    }
}
