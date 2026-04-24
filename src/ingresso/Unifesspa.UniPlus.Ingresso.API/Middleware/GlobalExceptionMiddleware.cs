namespace Unifesspa.UniPlus.Ingresso.API.Middleware;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using FluentValidation;

using Microsoft.AspNetCore.Mvc;

internal sealed partial class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
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
            await _next(context);
        }
        catch (ValidationException ex)
        {
            LogValidationError(_logger, context.Request.Path, ex);
            await EscreverRespostaValidacao(context, ex);
        }
        catch (Exception ex)
        {
            LogUnhandledError(_logger, context.Request.Path, ex);
            await EscreverRespostaErro(context);
        }
    }

    private static async Task EscreverRespostaValidacao(HttpContext context, ValidationException exception)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = "application/problem+json";

        Dictionary<string, string[]> errors = exception.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

        ValidationProblemDetails problem = new(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Erro de validação",
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1"
        };

        await context.Response.WriteAsJsonAsync(problem, JsonSerializerOptions.Default);
    }

    private static async Task EscreverRespostaErro(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        ProblemDetails problem = new()
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Erro interno do servidor",
            Detail = "Ocorreu um erro inesperado. Tente novamente mais tarde.",
            Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1"
        };

        await context.Response.WriteAsJsonAsync(problem, JsonSerializerOptions.Default);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Erro de validação no request {Path}")]
    private static partial void LogValidationError(ILogger logger, PathString path, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Erro não tratado no request {Path}")]
    private static partial void LogUnhandledError(ILogger logger, PathString path, Exception ex);
}
