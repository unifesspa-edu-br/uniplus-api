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

using Persistence;

using Unifesspa.UniPlus.Authorization.Abstractions;

using Unifesspa.UniPlus.Kernel.Results;

public sealed partial class GlobalExceptionMiddleware
{
    private static readonly JsonSerializerOptions WebJsonOptions = ProblemaNaResposta.OpcoesJson;

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
        catch (Exception ex)
        {
            TraducaoDeExcecao traducao = Traducoes.FirstOrDefault(t => t.Tipo.IsInstanceOfType(ex)) ?? NaoTratada;
            traducao.Registrar(_logger, context.Request.Path, ex);
            await traducao.Escrever(context, ex, _problemTypeUriFactory, _domainErrorMapper).ConfigureAwait(false);
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

        await context.Response
            .WriteAsJsonAsync(
                CorpoDaRespostaValidacao(exception, domainErrorMapper, status, type, title, code),
                WebJsonOptions,
                contentType: "application/problem+json")
            .ConfigureAwait(false);
    }

    private static Dictionary<string, object?> CorpoDaRespostaValidacao(
        ValidationException exception, IDomainErrorMapper domainErrorMapper, int status, string type, string title, string code) =>
        new()
        {
            ["type"] = type,
            ["title"] = title,
            ["status"] = status,
            ["instance"] = $"urn:uuid:{Guid.CreateVersion7()}",
            ["code"] = code,
            ["traceId"] = Activity.Current?.TraceId.ToHexString() ?? Guid.CreateVersion7().ToString("N"),
            ["errors"] = ErrosDeValidacao(exception, domainErrorMapper),
        };

    // Invariante: e.ErrorMessage não deve conter PII nem o valor rejeitado.
    // Usar {PropertyValue} em templates FluentValidation viola essa restrição.
    // O ErrorCode já chega na taxonomia (ValidationErrorCodes.Resolve, ou .WithErrorCode explícito);
    // o mapper publica o código de wire dele.
    private static object[] ErrosDeValidacao(ValidationException exception, IDomainErrorMapper domainErrorMapper) =>
        [.. exception.Errors.Select(e => new
        {
            field = NomeDeCampoNoPayload.De(e.PropertyName),
            code = DomainErrorProblemDetailsFactory.Resolve(
                new DomainError(e.ErrorCode ?? ValidationErrorCodes.Regra, e.ErrorMessage), domainErrorMapper).Code,
            message = e.ErrorMessage,
        })];

    private static Task EscreverRespostaConflitoDeConcorrencia(HttpContext context, IProblemTypeUriFactory problemTypeUriFactory) =>
        ProblemaNaResposta.EscreverAsync(
            context,
            problemTypeUriFactory,
            ErrosDaFronteiraHttp.ConflitoDeConcorrencia,
            "Este recurso foi modificado por outra operação concorrente. Recarregue os dados e tente novamente.");

    private static Task EscreverRespostaCursorInvalido(HttpContext context, IProblemTypeUriFactory problemTypeUriFactory) =>
        ProblemaNaResposta.EscreverAsync(
            context,
            problemTypeUriFactory,
            ErrosDaFronteiraHttp.CursorInvalido,
            "O cursor informado não continua esta consulta. Refaça a listagem sem cursor.");

    // O registro de acesso é do shared e qualquer módulo pode lançar a falha, então a resposta não depende do registro
    // de erros de um módulo. A causa não vai à resposta.
    private static Task EscreverRespostaRegistroDeAcessoNaoGravado(HttpContext context, IProblemTypeUriFactory problemTypeUriFactory) =>
        ProblemaNaResposta.EscreverAsync(
            context,
            problemTypeUriFactory,
            ErrosDaFronteiraHttp.RegistroDeAcessoNaoGravado,
            "O acesso não pôde ser registrado e por isso não foi concluído. Tente novamente em instantes.");

    // O catálogo é do shared e qualquer módulo que decide acesso pode lançar a falha; a resposta não depende do registro de
    // erros de um módulo.
    private static Task EscreverRespostaCatalogoDesatualizado(HttpContext context, IProblemTypeUriFactory problemTypeUriFactory) =>
        ProblemaNaResposta.EscreverAsync(
            context,
            problemTypeUriFactory,
            ErrosDaFronteiraHttp.CatalogoDePermissoesDesatualizado,
            "O sistema está sendo atualizado e o acesso não pôde ser decidido. Tente novamente em instantes.");

    // A trava é do banco compartilhado e qualquer módulo pode esperar por ela; a resposta não depende do registro de erros
    // de um módulo.
    private static Task EscreverRespostaEsperaPelaTravaEsgotada(HttpContext context, IProblemTypeUriFactory problemTypeUriFactory) =>
        RespostaDaEsperaEsgotadaPelaTrava.EscreverAsync(context, problemTypeUriFactory);

    private static Task EscreverRespostaErro(HttpContext context, IProblemTypeUriFactory problemTypeUriFactory) =>
        ProblemaNaResposta.EscreverAsync(
            context,
            problemTypeUriFactory,
            ErrosDaFronteiraHttp.ErroInesperado,
            "Ocorreu um erro inesperado. Tente novamente mais tarde.");

    [LoggerMessage(Level = LogLevel.Warning, Message = "Erro de validação no request {Path}")]
    private static partial void LogValidationError(ILogger logger, PathString path, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Conflito de concorrência otimista no request {Path}")]
    private static partial void LogConflitoDeConcorrencia(ILogger logger, PathString path, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Âncora de cursor incompatível com a ordenação no request {Path}")]
    private static partial void LogAncoraDeCursorInvalida(ILogger logger, PathString path, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Registro de acesso não gravado no request {Path}")]
    private static partial void LogRegistroDeAcessoNaoGravado(ILogger logger, PathString path, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Catálogo de permissões do banco diferente do desta instância no request {Path}")]
    private static partial void LogCatalogoDesatualizado(ILogger logger, PathString path, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Espera pela trava esgotada no request {Path}")]
    private static partial void LogEsperaPelaTravaEsgotada(ILogger logger, PathString path, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Erro não tratado no request {Path}")]
    private static partial void LogUnhandledError(ILogger logger, PathString path, Exception ex);
}
