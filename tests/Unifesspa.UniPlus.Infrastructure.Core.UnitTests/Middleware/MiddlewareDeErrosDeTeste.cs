namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Middleware;

using System.Text.Json;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Middleware;

/// <summary>O middleware de erros montado com o registro de erros do kernel, e o contexto HTTP em memória que o exercita.</summary>
internal static class MiddlewareDeErrosDeTeste
{
    internal const string BaseUriDoCatalogo = "https://unifesspa-edu-br.github.io/uniplus-developers/erros/";

    internal static GlobalExceptionMiddleware Criar(RequestDelegate next) =>
        Criar(next, Substitute.For<ILogger<GlobalExceptionMiddleware>>());

    internal static GlobalExceptionMiddleware Criar(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        ProblemTypeUriFactory problemTypeUriFactory = new(
            Options.Create(new ProblemTypeOptions { BaseUri = BaseUriDoCatalogo }));
        DomainErrorMappingRegistry mapper = new([new KernelDomainErrorRegistration()], problemTypeUriFactory);
        return new GlobalExceptionMiddleware(next, logger, problemTypeUriFactory, mapper);
    }

    internal static DefaultHttpContext CriarContexto()
    {
        DefaultHttpContext context = new();
        context.Response.Body = new MemoryStream();
        return context;
    }

    internal static async Task<JsonDocument> LerBodyAsync(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        return await JsonDocument.ParseAsync(context.Response.Body);
    }
}
