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
/// action (ou a execução/serialização do resultado que ela devolveu) lança sem
/// que nenhum filtro marque <c>ExceptionHandled</c>.
/// </summary>
/// <remarks>
/// <para>Instancia o filtro diretamente (não via HTTP completo) para controlar o
/// <c>next()</c> com precisão: nenhum endpoint de produção deste monólito
/// deveria, por design (ADR-0046, ADR-0119), deixar uma exceção não tratada
/// escapar de um handler atrás de <c>[RequiresIdempotencyKey]</c> — é
/// exatamente por isso que esse caminho nunca foi exercitado antes deste
/// achado. O teste usa dependências reais (store EF Core contra Postgres
/// efêmero, criptografia real) e só substitui <see cref="IUserContext"/> (não
/// há uma requisição HTTP real autenticando via Keycloak aqui).</para>
/// <para><b>Por que a reservation não é liberada</b> (revisão de PR): o
/// <c>next()</c> cobre tanto a execução da action quanto a
/// execução/serialização do <c>IActionResult</c> que ela devolveu — uma
/// exceção pendente pode ter surgido DEPOIS que o comando já persistiu (ex.:
/// falha ao serializar a resposta). Sem saber em qual etapa a exceção nasceu,
/// apagar a reservation permitiria que um retry do cliente com a mesma chave
/// reexecutasse uma mutação já aplicada. A política é a mesma já aceita pela
/// ADR-0027 §"Atomicidade parcial" para falhas pós-handler: a entrada fica em
/// <c>Processing</c> até o TTL, e o cliente recebe 409 <c>ProcessingConflict</c>
/// num retry imediato — não uma resposta fabricada, mas também não uma
/// reexecução da mutação.</para>
/// </remarks>
[Collection(ConfiguracaoEndpointCollection.Name)]
[Trait("Category", "Integration")]
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
        "Exceção pendente e não tratada no next() não é cacheada como sucesso — reservation fica em Processing, não é liberada")]
    public async Task OnResourceExecutionAsync_WithPendingException_DoesNotCacheAndKeepsProcessing()
    {
        MonolitoApiFactory api = _fixture.Factory;
        string idempotencyKey = Guid.NewGuid().ToString();
        string endpoint = "POST /api/configuracao/admin/termos-consentimento/00000000-0000-0000-0000-000000000001/revisar";

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

        MethodInfo idempotentMethod = typeof(TermosConsentimentoController)
            .GetMethod(nameof(TermosConsentimentoController.MarcarRevisado))!;
        ControllerActionDescriptor actionDescriptor = new()
        {
            MethodInfo = idempotentMethod,
            ControllerTypeInfo = typeof(TermosConsentimentoController).GetTypeInfo(),
        };

        DefaultHttpContext httpContext = new()
        {
            RequestServices = services,
        };
        httpContext.Request.Method = "POST";
        httpContext.Request.Path = "/api/configuracao/admin/termos-consentimento/00000000-0000-0000-0000-000000000001/revisar";
        httpContext.Request.Headers["Idempotency-Key"] = idempotencyKey;
        byte[] body = Encoding.UTF8.GetBytes("""{"texto":"corpo de teste"}""");
        httpContext.Request.Body = new MemoryStream(body);
        httpContext.Request.ContentLength = body.Length;
        httpContext.Response.Body = new MemoryStream();

        ActionContext actionContext = new(httpContext, new RouteData(), actionDescriptor);
        List<IFilterMetadata> filters = [];

        // --- Primeira chamada: next() simula uma exceção não tratada por
        // nenhum filtro mais interno (o cenário real que o ResourceInvoker
        // produz quando a action — ou a serialização do resultado dela —
        // lança sem que ninguém marque ExceptionHandled). A chamada ao filtro
        // em si NÃO relança (isso é responsabilidade do ResourceInvoker
        // externo, não simulado aqui): o filtro só decide se cacheia ou não.
        ResourceExecutingContext executingContext = new(actionContext, filters, []);
        static Task<ResourceExecutedContext> NextWithPendingException(ActionContext ctx, IList<IFilterMetadata> f)
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
            () => NextWithPendingException(actionContext, filters));

        // O bodyHash do lookup precisa bater com o mesmo corpo — o filtro já
        // reposicionou o stream do request para 0 internamente.
        string bodyHash = ComputeBodyHash(body);
        string idempotencyScope = "user:idempotency-filter-exception-test-user";

        IdempotencyLookupResult lookupAfterException = await store.LookupAsync(
            idempotencyScope, endpoint, idempotencyKey, bodyHash, CancellationToken.None);

        lookupAfterException.Outcome.Should().Be(
            IdempotencyOutcome.Processing,
            "uma exceção pendente e não tratada não deve deixar entrada Completed (200 fabricado) nem ser apagada — "
            + "apagar arriscaria reexecutar uma mutação que já pode ter persistido antes da exceção");

        // --- Segunda chamada: retry imediato do cliente com a MESMA chave —
        // prova que ele recebe 409 ProcessingConflict (não uma reexecução da
        // mutação, não uma resposta fabricada). O filtro faz o short-circuit
        // antes de chamar next(), então o `next` desta chamada nunca deveria
        // ser invocado.
        DefaultHttpContext httpContext2 = new() { RequestServices = services };
        httpContext2.Request.Method = "POST";
        httpContext2.Request.Path = "/api/configuracao/admin/termos-consentimento/00000000-0000-0000-0000-000000000001/revisar";
        httpContext2.Request.Headers["Idempotency-Key"] = idempotencyKey;
        httpContext2.Request.Body = new MemoryStream(body);
        httpContext2.Request.ContentLength = body.Length;
        httpContext2.Response.Body = new MemoryStream();

        ActionContext actionContext2 = new(httpContext2, new RouteData(), actionDescriptor);
        ResourceExecutingContext executingContext2 = new(actionContext2, filters, []);

        bool nextInvoked = false;
        Task<ResourceExecutedContext> NextShouldNeverRun()
        {
            nextInvoked = true;
            return Task.FromResult(new ResourceExecutedContext(actionContext2, filters));
        }

        await filter.OnResourceExecutionAsync(executingContext2, NextShouldNeverRun);

        nextInvoked.Should().BeFalse(
            "o retry imediato deve ser rejeitado pelo lookup de Processing, sem nunca chegar a invocar a action de novo");
        executingContext2.Result.Should().NotBeNull(
            "o filtro deve ter produzido a resposta 409 ProcessingConflict via short-circuit");
    }

    private static string ComputeBodyHash(byte[] bytes)
    {
        Span<byte> hash = stackalloc byte[32];
        System.Security.Cryptography.SHA256.HashData(bytes, hash);
        return Convert.ToHexStringLower(hash);
    }
}
