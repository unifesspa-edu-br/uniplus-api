namespace Unifesspa.UniPlus.Infrastructure.Core.Middleware;

using System.Diagnostics.CodeAnalysis;

using Errors;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

public sealed class GlobalExceptionMiddleware
{
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
            TraducaoDeExcecao traducao = TraducoesDeExcecao.Da(ex);
            traducao.Registrar(_logger, context.Request.Path, ex);
            await traducao.Escrever(context, ex, _problemTypeUriFactory, _domainErrorMapper).ConfigureAwait(false);
        }
    }
}
