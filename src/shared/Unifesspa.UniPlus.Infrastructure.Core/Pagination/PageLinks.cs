namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

using System.Globalization;

using Microsoft.AspNetCore.Http;

/// <summary>
/// URLs de navegação RFC 5988/8288 de uma página: <c>self</c> sempre presente;
/// <c>next</c>/<c>prev</c> ausentes quando o cursor correspondente não existe
/// (extremos da lista).
/// </summary>
public sealed record PageLinks(string Self, string? Next, string? Prev)
{
    /// <summary>
    /// Self espelha a requisição original (cursor, limit e direction como o leitor da página os lê: o primeiro valor,
    /// sem os em branco). Prev e Next NÃO incluem limit (o cursor já carrega o tamanho da página, ajustado na
    /// leitura) e carregam a direção explícita: a navegação fica auto-suficiente (RFC 5988) e o cliente segue o link
    /// opaco sem conhecer a convenção. Os três repetem só os <paramref name="filtros"/> que a listagem declara.
    /// </summary>
    internal static PageLinks ForRequest(
        HttpRequest request, string? previousCursor, string? nextCursor, IReadOnlyList<string> filtros) => new(
        Self: SelfOf(request, filtros),
        Next: nextCursor is null ? null : BuildLink(request, nextCursor, limit: null, "next", filtros),
        Prev: previousCursor is null ? null : BuildLink(request, previousCursor, limit: null, "prev", filtros));

    private static string SelfOf(HttpRequest request, IReadOnlyList<string> filtros) => BuildLink(
        request,
        PrimeiroValor(request.Query, "cursor"),
        LimitAsRequested(request.Query),
        PrimeiroValor(request.Query, "direction"),
        filtros);

    private static int? LimitAsRequested(IQueryCollection query) =>
        int.TryParse(PrimeiroValor(query, "limit"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : null;

    private static string? PrimeiroValor(IQueryCollection query, string nome)
    {
        string? valor = query[nome].FirstOrDefault();
        return string.IsNullOrWhiteSpace(valor) ? null : valor;
    }

    private static string BuildLink(HttpRequest request, string? cursor, int? limit, string? direction, IReadOnlyList<string> filtros)
    {
        // Inclui PathBase para honrar deploys atrás de reverse proxy
        // (Traefik, nginx, ingress) ou hosts com app.UsePathBase("/foo").
        // Sem isso, links de navegação apontam para /api/editais em vez
        // de /foo/api/editais e clientes recebem 404 ao seguir Link header.
        string baseUrl = $"{request.Scheme}://{request.Host}{request.PathBase}{request.Path}";
        List<string> parts = [.. ParametrosDePaginacao(cursor, limit, direction), .. filtros];

        return parts.Count == 0 ? baseUrl : $"{baseUrl}?{string.Join('&', parts)}";
    }

    private static IEnumerable<string> ParametrosDePaginacao(string? cursor, int? limit, string? direction)
    {
        if (cursor is not null)
            yield return $"cursor={Uri.EscapeDataString(cursor)}";
        if (limit is { } l)
            yield return $"limit={l.ToString(CultureInfo.InvariantCulture)}";
        if (direction is not null)
            yield return $"direction={Uri.EscapeDataString(direction)}";
    }
}
