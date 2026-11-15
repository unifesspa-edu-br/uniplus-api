namespace Unifesspa.UniPlus.Infrastructure.Core.Authentication;

using System.Diagnostics;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>
/// Writer compartilhado para respostas <c>application/problem+json</c> originadas
/// na camada de autenticação/autorização (RFC 9457). Centraliza shape, type URLs e
/// extensions (<c>code</c>, <c>traceId</c>, <c>instance</c>) para que JwtBearer
/// (produção) e handlers de teste produzam payloads byte-equivalentes.
/// </summary>
public static class AuthenticationProblemDetailsWriter
{
    private const string UnauthorizedDetail =
        "Requisição requer autenticação válida. Inclua um token Bearer não expirado.";
    private const string ForbiddenDetail =
        "Token autenticado, mas sem permissão para acessar este recurso.";

    /// <summary>
    /// Escreve um body 401 problem+json no <paramref name="httpContext"/>.
    /// Adiciona <c>WWW-Authenticate: Bearer</c> se ausente — RFC 7235 §4.1 e
    /// RFC 9110 §11.6.1 exigem o header em toda resposta 401.
    /// </summary>
    public static Task WriteUnauthorizedAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        // Sem realm/error/error_description — política Uni+ não expõe motivo
        // de falha JWT no canal de resposta (LGPD + ADR-0034).
        if (!httpContext.Response.HasStarted
            && !httpContext.Response.Headers.ContainsKey(HeaderNames.WWWAuthenticate))
        {
            httpContext.Response.Headers.Append(HeaderNames.WWWAuthenticate, "Bearer");
        }

        return WriteAsync(httpContext, ErrosDaFronteiraHttp.NaoAutenticado, UnauthorizedDetail);
    }

    /// <summary>
    /// Escreve um body 403 problem+json no <paramref name="httpContext"/>.
    /// </summary>
    public static Task WriteForbiddenAsync(HttpContext httpContext) =>
        WriteAsync(httpContext, ErrosDaFronteiraHttp.AcessoNegado, ForbiddenDetail);

    private static async Task WriteAsync(
        HttpContext httpContext,
        DomainErrorMapping definicao,
        string detail)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (httpContext.Response.HasStarted)
            return;

        httpContext.Response.StatusCode = definicao.Status;

        IProblemTypeUriFactory problemTypeUriFactory = httpContext.RequestServices
            .GetRequiredService<IProblemTypeUriFactory>();

        ProblemDetails problem = new()
        {
            Status = definicao.Status,
            Type = problemTypeUriFactory.Build(definicao.Code),
            Title = definicao.Title,
            Detail = detail,
            Instance = $"urn:uuid:{Guid.CreateVersion7()}",
        };

        problem.Extensions["code"] = definicao.Code;
        problem.Extensions["traceId"] = Activity.Current?.TraceId.ToHexString()
            ?? Guid.CreateVersion7().ToString("N");

        IProblemDetailsService? service = httpContext.RequestServices
            .GetService<IProblemDetailsService>();

        if (service is not null)
        {
            ProblemDetailsContext problemContext = new()
            {
                HttpContext = httpContext,
                ProblemDetails = problem,
            };

            if (await service.TryWriteAsync(problemContext).ConfigureAwait(false))
                return;
        }

        // Fallback caso AddProblemDetails não tenha sido registrado: escreve
        // o body manualmente preservando o contrato application/problem+json.
        // O overload <c>WriteAsJsonAsync&lt;TValue&gt;(response, value, contentType)</c>
        // é obrigatório aqui — a sobrecarga sem contentType reescreve o header
        // como "application/json; charset=utf-8" mesmo após Response.ContentType
        // ter sido setado, quebrando o contrato problem+json.
        await httpContext.Response.WriteAsJsonAsync(
                problem,
                options: (System.Text.Json.JsonSerializerOptions?)null,
                contentType: "application/problem+json")
            .ConfigureAwait(false);
    }
}
