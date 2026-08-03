namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Idempotency;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Configuracao.API.Controllers;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// Prova de correção do fix da issue #1028: <see cref="IdempotencyFilter{TDbContext}"/>
/// não deve cachear uma resposta de sucesso quando o <c>next()</c> devolve um
/// <see cref="ResourceExecutedContext"/> com uma exceção pendente e não tratada
/// — o cenário que o <c>ResourceInvoker</c> do ASP.NET Core MVC produz quando a
/// action (ou algo mais interno na pipeline) lança sem que nenhum filtro marque
/// <c>ExceptionHandled</c>.
/// </summary>
/// <remarks>
/// Instancia o filtro diretamente (não via HTTP completo) para controlar o
/// <c>next()</c> com precisão: nenhum endpoint de produção deste monólito
/// deveria, por design (ADR-0046, ADR-0119), deixar uma exceção não tratada
/// escapar de um handler atrás de <c>[RequiresIdempotencyKey]</c> — é
/// exatamente por isso que esse caminho nunca foi exercitado antes deste
/// achado. O teste usa dependências reais (store EF Core contra Postgres
/// efêmero, criptografia real) e só substitui <see cref="IUserContext"/> (não
/// há uma requisição HTTP real autenticando via Keycloak aqui).
/// </remarks>
[Collection(ConfiguracaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class IdempotencyFilterExceptionTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public IdempotencyFilterExceptionTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName =
        "Exceção pendente e não tratada no next() não é cacheada como sucesso, e a reservation é liberada")]
    public async Task OnResourceExecutionAsync_ComExcecaoPendente_NaoCachearELiberaReservation()
    {
        MonolitoApiFactory api = _fixture.Factory;
        string idempotencyKey = Guid.NewGuid().ToString();
        string endpoint = "POST /api/configuracao/admin/termos-consentimento/idempotency-filter-exception-test";

        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        IServiceProvider services = scope.ServiceProvider;

        EfCoreIdempotencyStore<ConfiguracaoDbContext> store = ActivatorUtilities
            .CreateInstance<EfCoreIdempotencyStore<ConfiguracaoDbContext>>(services);
        IUniPlusEncryptionService encryption = services.GetRequiredService<IUniPlusEncryptionService>();
        IDomainErrorMapper errorMapper = services.GetRequiredService<IDomainErrorMapper>();
        TimeProvider time = services.GetRequiredService<TimeProvider>();
        IOptions<IdempotencyOptions> options = services.GetRequiredService<IOptions<IdempotencyOptions>>();

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.IsAuthenticated.Returns(true);
        userContext.UserId.Returns("idempotency-filter-exception-test-user");

        IdempotencyFilter<ConfiguracaoDbContext> filter = new(store, encryption, errorMapper, time, userContext, options);

        MethodInfo metodoIdempotente = typeof(TermosConsentimentoController)
            .GetMethod(nameof(TermosConsentimentoController.EditarRascunho))!;
        ControllerActionDescriptor actionDescriptor = new()
        {
            MethodInfo = metodoIdempotente,
            ControllerTypeInfo = typeof(TermosConsentimentoController).GetTypeInfo(),
        };

        DefaultHttpContext httpContext = new()
        {
            RequestServices = services,
        };
        httpContext.Request.Method = "POST";
        httpContext.Request.Path = "/api/configuracao/admin/termos-consentimento/idempotency-filter-exception-test";
        httpContext.Request.Headers["Idempotency-Key"] = idempotencyKey;
        byte[] corpo = Encoding.UTF8.GetBytes("""{"texto":"corpo de teste"}""");
        httpContext.Request.Body = new MemoryStream(corpo);
        httpContext.Request.ContentLength = corpo.Length;
        httpContext.Response.Body = new MemoryStream();

        ActionContext actionContext = new(httpContext, new RouteData(), actionDescriptor);
        List<IFilterMetadata> filtros = [];

        // --- Primeira chamada: next() simula uma exceção não tratada por
        // nenhum filtro mais interno (o cenário real que o ResourceInvoker
        // produz quando a action lança sem que ninguém marque ExceptionHandled).
        ResourceExecutingContext executingContext = new(actionContext, filtros, []);
        static Task<ResourceExecutedContext> NextComExcecaoPendente(ActionContext ctx, IList<IFilterMetadata> f)
        {
            ResourceExecutedContext executed = new(ctx, f)
            {
                Exception = new InvalidOperationException("erro sintético não relacionado a idempotência"),
                ExceptionHandled = false,
            };
            return Task.FromResult(executed);
        }

        await filter.OnResourceExecutionAsync(
            executingContext,
            () => NextComExcecaoPendente(actionContext, filtros));

        // O bodyHash do lookup precisa bater com o mesmo corpo — o filtro já
        // reposicionou o stream do request para 0 internamente.
        string bodyHash = ComputarBodyHash(corpo);
        string scopeChave = $"user:idempotency-filter-exception-test-user";

        IdempotencyLookupResult lookupAposExcecao = await store.LookupAsync(
            scopeChave, endpoint, idempotencyKey, bodyHash, CancellationToken.None);

        lookupAposExcecao.Outcome.Should().Be(
            IdempotencyOutcome.Miss,
            "uma exceção pendente e não tratada não deve deixar entrada cacheada — nem Completed (200 fabricado), nem Processing pendurada");

        // --- Segunda chamada: replay da MESMA chave, desta vez com sucesso —
        // prova que a reservation foi de fato liberada, não deixada presa em
        // Processing (o que devolveria 409 ProcessingConflict indefinidamente).
        DefaultHttpContext httpContext2 = new() { RequestServices = services };
        httpContext2.Request.Method = "POST";
        httpContext2.Request.Path = "/api/configuracao/admin/termos-consentimento/idempotency-filter-exception-test";
        httpContext2.Request.Headers["Idempotency-Key"] = idempotencyKey;
        httpContext2.Request.Body = new MemoryStream(corpo);
        httpContext2.Request.ContentLength = corpo.Length;
        httpContext2.Response.Body = new MemoryStream();

        ActionContext actionContext2 = new(httpContext2, new RouteData(), actionDescriptor);
        ResourceExecutingContext executingContext2 = new(actionContext2, filtros, []);

        static Task<ResourceExecutedContext> NextComSucesso(ActionContext ctx, IList<IFilterMetadata> f, HttpContext http)
        {
            http.Response.StatusCode = StatusCodes.Status204NoContent;
            ResourceExecutedContext executed = new(ctx, f);
            return Task.FromResult(executed);
        }

        await filter.OnResourceExecutionAsync(
            executingContext2,
            () => NextComSucesso(actionContext2, filtros, httpContext2));

        IdempotencyLookupResult lookupAposSucesso = await store.LookupAsync(
            scopeChave, endpoint, idempotencyKey, bodyHash, CancellationToken.None);

        lookupAposSucesso.Outcome.Should().Be(
            IdempotencyOutcome.HitMatch,
            "depois que a exceção liberou a reservation, um segundo request com a mesma chave deve rodar e cachear normalmente — não ficar preso em Processing");
    }

    private static string ComputarBodyHash(byte[] bytes)
    {
        Span<byte> hash = stackalloc byte[32];
        System.Security.Cryptography.SHA256.HashData(bytes, hash);
        return Convert.ToHexStringLower(hash);
    }
}
