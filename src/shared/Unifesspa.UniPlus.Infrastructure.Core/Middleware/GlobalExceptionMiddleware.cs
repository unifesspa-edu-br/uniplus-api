namespace Unifesspa.UniPlus.Infrastructure.Core.Middleware;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Errors;

using FluentValidation;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Pagination;

using Unifesspa.UniPlus.Kernel.Results;

public sealed partial class GlobalExceptionMiddleware
{
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IProblemTypeUriFactory _problemTypeUriFactory;
    private readonly IDomainErrorMapper _domainErrorMapper;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IProblemTypeUriFactory problemTypeUriFactory,
        IDomainErrorMapper domainErrorMapper)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(problemTypeUriFactory);
        ArgumentNullException.ThrowIfNull(domainErrorMapper);
        _next = next;
        _logger = logger;
        _problemTypeUriFactory = problemTypeUriFactory;
        _domainErrorMapper = domainErrorMapper;
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
            await EscreverRespostaValidacao(context, ex, _domainErrorMapper).ConfigureAwait(false);
        }
        catch (CursorAnchorMismatchException ex)
        {
            LogAncoraDeCursorInvalida(_logger, context.Request.Path, ex);
            await EscreverRespostaCursorInvalido(context, _problemTypeUriFactory).ConfigureAwait(false);
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
        IDomainErrorMapper domainErrorMapper)
    {
        // code/type/title pelo mapper, como todo emissor de erro (ADR-0024): o 422 de
        // validação deixa de ser o único trio escrito à mão, fora do catálogo.
        (int status, string type, string title, string code, bool _) = DomainErrorProblemDetailsFactory.Resolve(
            new DomainError(ValidationErrorCodes.Raiz, "Erro de validação"), domainErrorMapper);

        context.Response.StatusCode = status;

        Dictionary<string, object?> body = new()
        {
            ["type"] = type,
            ["title"] = title,
            ["status"] = status,
            ["instance"] = $"urn:uuid:{Guid.CreateVersion7()}",
            ["code"] = code,
            ["traceId"] = Activity.Current?.TraceId.ToHexString() ?? Guid.CreateVersion7().ToString("N"),
            // Invariante: e.ErrorMessage não deve conter PII nem o valor rejeitado.
            // Usar {PropertyValue} em templates FluentValidation viola essa restrição.
            ["errors"] = exception.Errors
                .Select(e => new
                {
                    field = NomeDeCampoNoPayload(e.PropertyName),
                    // O ErrorCode já chega na taxonomia (ValidationErrorCodes.Resolve, ou
                    // .WithErrorCode explícito); o mapper publica o código de wire dele.
                    code = DomainErrorProblemDetailsFactory.Resolve(
                        new DomainError(e.ErrorCode ?? ValidationErrorCodes.Regra, e.ErrorMessage), domainErrorMapper).Code,
                    message = e.ErrorMessage,
                })
                .ToArray(),
        };

        await context.Response
            .WriteAsJsonAsync(body, WebJsonOptions, contentType: "application/problem+json")
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Traduz o caminho de propriedade do FluentValidation (PascalCase, nome C#) para o casing
    /// do payload JSON — o mesmo que o caminho de validação do domínio já emite (ADR-0125) e
    /// que o frontend usa para achar o controle do formulário. A conversão é por segmento,
    /// preservando o índice: <c>Quadro[0].Quantidade</c> vira <c>quadro[0].quantidade</c>.
    /// </summary>
    private static string NomeDeCampoNoPayload(string? propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
        {
            return string.Empty;
        }

        return string.Join('.', propertyName.Split('.').Select(static segmento =>
        {
            int indice = segmento.IndexOf('[', StringComparison.Ordinal);
            return indice < 0
                ? JsonNamingPolicy.CamelCase.ConvertName(segmento)
                : JsonNamingPolicy.CamelCase.ConvertName(segmento[..indice]) + segmento[indice..];
        }));
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

    private static async Task EscreverRespostaCursorInvalido(
        HttpContext context,
        IProblemTypeUriFactory problemTypeUriFactory)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;

        Dictionary<string, object?> body = new()
        {
            ["type"] = problemTypeUriFactory.Build("uniplus.paginacao.cursor-invalido"),
            ["title"] = "Cursor inválido",
            ["status"] = StatusCodes.Status400BadRequest,
            ["detail"] = "O cursor informado não continua esta consulta. Refaça a listagem sem cursor.",
            ["instance"] = $"urn:uuid:{Guid.CreateVersion7()}",
            ["code"] = "uniplus.paginacao.cursor-invalido",
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Âncora de cursor incompatível com a ordenação no request {Path}")]
    private static partial void LogAncoraDeCursorInvalida(ILogger logger, PathString path, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Erro não tratado no request {Path}")]
    private static partial void LogUnhandledError(ILogger logger, PathString path, Exception ex);
}
