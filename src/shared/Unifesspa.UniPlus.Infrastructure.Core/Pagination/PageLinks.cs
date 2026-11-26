namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

using System.Globalization;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Application.Abstractions.Consultas;

/// <summary>
/// URLs de navegação RFC 5988/8288 de uma página: <c>self</c> sempre presente;
/// <c>next</c>/<c>prev</c> ausentes quando o cursor correspondente não existe
/// (extremos da lista).
/// </summary>
public sealed record PageLinks(string Self, string? Next, string? Prev)
{
    private static readonly string[] ReservedPaginationParams = ["cursor", "limit", "direction"];

    /// <summary>
    /// Self espelha a requisição original (cursor, limit e direction como vieram). Prev e Next NÃO incluem
    /// limit (o cursor já carrega o tamanho da página, ajustado na leitura) e carregam a direção explícita:
    /// a navegação fica auto-suficiente (RFC 5988) e o cliente segue o link opaco sem conhecer a convenção.
    /// </summary>
    internal static PageLinks ForRequest(HttpRequest request, string? previousCursor, string? nextCursor) => new(
        Self: SelfOf(request),
        Next: nextCursor is null ? null : BuildLink(request, nextCursor, limit: null, "next"),
        Prev: previousCursor is null ? null : BuildLink(request, previousCursor, limit: null, "prev"));

    private static string SelfOf(HttpRequest request) => BuildLink(
        request,
        request.Query["cursor"],
        LimitAsRequested(request.Query),
        request.Query["direction"]);

    private static int? LimitAsRequested(IQueryCollection query)
    {
        string? requested = query["limit"];
        return int.TryParse(requested, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : null;
    }

    private static string BuildLink(HttpRequest request, string? cursor, int? limit, string? direction)
    {
        // Inclui PathBase para honrar deploys atrás de reverse proxy
        // (Traefik, nginx, ingress) ou hosts com app.UsePathBase("/foo").
        // Sem isso, links de navegação apontam para /api/editais em vez
        // de /foo/api/editais e clientes recebem 404 ao seguir Link header.
        string baseUrl = $"{request.Scheme}://{request.Host}{request.PathBase}{request.Path}";
        List<string> parts = [.. ParametrosDePaginacao(cursor, limit, direction), .. FiltrosDaRequisicao(request)];

        return parts.Count == 0 ? baseUrl : $"{baseUrl}?{string.Join('&', parts)}";
    }

    private static IEnumerable<string> ParametrosDePaginacao(string? cursor, int? limit, string? direction)
    {
        if (!string.IsNullOrEmpty(cursor))
            yield return $"cursor={Uri.EscapeDataString(cursor)}";
        if (limit is { } l)
            yield return $"limit={l.ToString(CultureInfo.InvariantCulture)}";
        if (!string.IsNullOrEmpty(direction))
            yield return $"direction={Uri.EscapeDataString(direction)}";
    }

    // Preserva os demais query params do request (filtros como q/tipo) nos
    // links self/next. Sem isso, seguir rel="next" numa listagem filtrada
    // voltaria a uma página SEM filtro (RFC 5988: o link deve ser
    // auto-suficiente). Os params de paginação são tratados em BuildLink;
    // multi-valores (ex.: tipo=3&tipo=4) são preservados.
    private static IEnumerable<string> FiltrosDaRequisicao(HttpRequest request) =>
        request.Query
            .Where(param => !ReservedPaginationParams.Contains(param.Key, StringComparer.OrdinalIgnoreCase))
            .SelectMany(param => param.Value
                .OfType<string>()
                .Select(value => $"{Uri.EscapeDataString(param.Key)}={Uri.EscapeDataString(ValorParaOLink(param.Key, value))}"));

    // Só o texto pesquisado volta sem os espaços das pontas, como a busca o aceita: o limite
    // de comprimento não os conta e o link não os repete. Os demais filtros voltam como vieram,
    // porque alguns leem o valor bruto e aparar mudaria a coleção.
    private static string ValorParaOLink(string nome, string valor) =>
        BuscaPedida.NomesDosParametros.Contains(nome, StringComparer.OrdinalIgnoreCase) ? valor.Trim() : valor;
}
