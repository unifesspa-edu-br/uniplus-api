namespace Unifesspa.UniPlus.Infrastructure.Core.Authentication;

using System.Diagnostics;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>
/// Writer compartilhado para respostas <c>application/problem+json</c> originadas
/// na camada de autenticação/autorização (RFC 9457). Centraliza shape, type URLs e
/// extensions (<c>code</c>, <c>traceId</c>, <c>instance</c>) para que JwtBearer
/// (produção) e handlers de teste produzam payloads byte-equivalentes.
/// </summary>
public static class AuthenticationProblemDetailsWriter
{
    /// <summary>
    /// Code/title/detail canônicos do 401 emitido pela camada de autenticação.
    /// </summary>
    public const string UnauthorizedCode = "uniplus.auth.unauthorized";

    /// <summary>
    /// Code/title/detail canônicos do 403 emitido pela camada de autorização.
    /// </summary>
    public const string ForbiddenCode = "uniplus.auth.forbidden";

    private const string UnauthorizedTitle = "Não autenticado";
    private const string ForbiddenTitle = "Acesso negado";
    private const string UnauthorizedDetail =
        "Requisição requer autenticação válida. Inclua um token Bearer não expirado.";
    private const string ForbiddenDetail =
        "Token autenticado, mas sem permissão para acessar este recurso.";

    /// <summary>
    /// Escreve um body 401 problem+json no <paramref name="httpContext"/>.
    /// </summary>
    public static Task WriteUnauthorizedAsync(HttpContext httpContext) =>
        WriteAsync(httpContext, StatusCodes.Status401Unauthorized,
            UnauthorizedCode, UnauthorizedTitle, UnauthorizedDetail);

    /// <summary>
    /// Escreve um body 403 problem+json no <paramref name="httpContext"/>.
    /// </summary>
    public static Task WriteForbiddenAsync(HttpContext httpContext) =>
        WriteAsync(httpContext, StatusCodes.Status403Forbidden,
            ForbiddenCode, ForbiddenTitle, ForbiddenDetail);

    private static async Task WriteAsync(
        HttpContext httpContext,
        int statusCode,
        string code,
        string title,
        string detail)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (httpContext.Response.HasStarted)
            return;

        httpContext.Response.StatusCode = statusCode;

        ProblemDetails problem = new()
        {
            Status = statusCode,
            Type = ProblemDetailsConstants.ErrorsBaseUri + code,
            Title = title,
            Detail = detail,
            Instance = $"urn:uuid:{Guid.CreateVersion7()}",
        };

        problem.Extensions["code"] = code;
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
