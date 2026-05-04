namespace Unifesspa.UniPlus.Infrastructure.Core.Middleware;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using FluentValidation;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

public sealed partial class GlobalExceptionMiddleware
{
    private const string ErrorsBaseUri = "https://errors.uniplus.unifesspa.edu.br/";
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(logger);
        _next = next;
        _logger = logger;
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Global exception boundary: unhandled exceptions must be converted to RFC 9457 ProblemDetails before bubbling out of the pipeline.")]
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            await _next(context).ConfigureAwait(false);
        }
        catch (ValidationException ex)
        {
            LogValidationError(_logger, context.Request.Path, ex);
            await EscreverRespostaValidacao(context, ex).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogUnhandledError(_logger, context.Request.Path, ex);
            await EscreverRespostaErro(context).ConfigureAwait(false);
        }
    }

    private static async Task EscreverRespostaValidacao(HttpContext context, ValidationException exception)
    {
        context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        context.Response.ContentType = "application/problem+json";

        var body = new Dictionary<string, object?>
        {
            ["type"] = ErrorsBaseUri + "uniplus.validacao",
            ["title"] = "Erro de validação",
            ["status"] = StatusCodes.Status422UnprocessableEntity,
            ["instance"] = $"urn:uuid:{Guid.CreateVersion7()}",
            ["code"] = "uniplus.validacao",
            ["traceId"] = Activity.Current?.TraceId.ToHexString() ?? Guid.CreateVersion7().ToString("N"),
            ["errors"] = exception.Errors
                .Select(static e => new { field = e.PropertyName, code = e.ErrorCode, message = e.ErrorMessage })
                .ToArray(),
        };

        await context.Response.WriteAsJsonAsync(body, WebJsonOptions).ConfigureAwait(false);
    }

    private static async Task EscreverRespostaErro(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        var body = new Dictionary<string, object?>
        {
            ["type"] = ErrorsBaseUri + "uniplus.internal.unexpected",
            ["title"] = "Erro interno do servidor",
            ["status"] = StatusCodes.Status500InternalServerError,
            ["detail"] = "Ocorreu um erro inesperado. Tente novamente mais tarde.",
            ["instance"] = $"urn:uuid:{Guid.CreateVersion7()}",
            ["code"] = "uniplus.internal.unexpected",
            ["traceId"] = Activity.Current?.TraceId.ToHexString() ?? Guid.CreateVersion7().ToString("N"),
        };

        await context.Response.WriteAsJsonAsync(body, WebJsonOptions).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Erro de validação no request {Path}")]
    private static partial void LogValidationError(ILogger logger, PathString path, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Erro não tratado no request {Path}")]
    private static partial void LogUnhandledError(ILogger logger, PathString path, Exception ex);
}
