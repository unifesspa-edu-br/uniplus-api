namespace Unifesspa.UniPlus.Infrastructure.Core.Middleware;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Errors;

using FluentValidation;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public sealed partial class GlobalExceptionMiddleware
{
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IProblemTypeUriFactory _problemTypeUriFactory;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IProblemTypeUriFactory problemTypeUriFactory)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(problemTypeUriFactory);
        _next = next;
        _logger = logger;
        _problemTypeUriFactory = problemTypeUriFactory;
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
            await EscreverRespostaValidacao(context, ex, _problemTypeUriFactory).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            LogConflitoDeConcorrencia(_logger, context.Request.Path, ex);
            await EscreverRespostaConflitoDeConcorrencia(context, _problemTypeUriFactory).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogUnhandledError(_logger, context.Request.Path, ex);
            await EscreverRespostaErro(context, _problemTypeUriFactory).ConfigureAwait(false);
        }
    }

    private static async Task EscreverRespostaValidacao(
        HttpContext context,
        ValidationException exception,
        IProblemTypeUriFactory problemTypeUriFactory)
    {
        context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;

        Dictionary<string, object?> body = new()
        {
            ["type"] = problemTypeUriFactory.Build("uniplus.validacao"),
            ["title"] = "Erro de validação",
            ["status"] = StatusCodes.Status422UnprocessableEntity,
            ["instance"] = $"urn:uuid:{Guid.CreateVersion7()}",
            ["code"] = "uniplus.validacao",
            ["traceId"] = Activity.Current?.TraceId.ToHexString() ?? Guid.CreateVersion7().ToString("N"),
            // Invariante: e.ErrorMessage não deve conter PII nem o valor rejeitado.
            // Usar {PropertyValue} em templates FluentValidation viola essa restrição.
            ["errors"] = exception.Errors
                .Select(static e => new { field = e.PropertyName, code = e.ErrorCode, message = e.ErrorMessage })
                .ToArray(),
        };

        await context.Response
            .WriteAsJsonAsync(body, WebJsonOptions, contentType: "application/problem+json")
            .ConfigureAwait(false);
    }

    private static async Task EscreverRespostaConflitoDeConcorrencia(
        HttpContext context,
        IProblemTypeUriFactory problemTypeUriFactory)
    {
        context.Response.StatusCode = StatusCodes.Status409Conflict;

        Dictionary<string, object?> body = new()
        {
            ["type"] = problemTypeUriFactory.Build("uniplus.concorrencia.conflito"),
            ["title"] = "Conflito de concorrência",
            ["status"] = StatusCodes.Status409Conflict,
            ["detail"] = "Este recurso foi modificado por outra operação concorrente. Recarregue os dados e tente novamente.",
            ["instance"] = $"urn:uuid:{Guid.CreateVersion7()}",
            ["code"] = "uniplus.concorrencia.conflito",
            ["traceId"] = Activity.Current?.TraceId.ToHexString() ?? Guid.CreateVersion7().ToString("N"),
        };

        await context.Response
            .WriteAsJsonAsync(body, WebJsonOptions, contentType: "application/problem+json")
            .ConfigureAwait(false);
    }

    private static async Task EscreverRespostaErro(
        HttpContext context,
        IProblemTypeUriFactory problemTypeUriFactory)
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        Dictionary<string, object?> body = new()
        {
            ["type"] = problemTypeUriFactory.Build("uniplus.internal.unexpected"),
            ["title"] = "Erro interno do servidor",
            ["status"] = StatusCodes.Status500InternalServerError,
            ["detail"] = "Ocorreu um erro inesperado. Tente novamente mais tarde.",
            ["instance"] = $"urn:uuid:{Guid.CreateVersion7()}",
            ["code"] = "uniplus.internal.unexpected",
            ["traceId"] = Activity.Current?.TraceId.ToHexString() ?? Guid.CreateVersion7().ToString("N"),
        };

        await context.Response
            .WriteAsJsonAsync(body, WebJsonOptions, contentType: "application/problem+json")
            .ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Erro de validação no request {Path}")]
    private static partial void LogValidationError(ILogger logger, PathString path, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Conflito de concorrência otimista no request {Path}")]
    private static partial void LogConflitoDeConcorrencia(ILogger logger, PathString path, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Erro não tratado no request {Path}")]
    private static partial void LogUnhandledError(ILogger logger, PathString path, Exception ex);
}
