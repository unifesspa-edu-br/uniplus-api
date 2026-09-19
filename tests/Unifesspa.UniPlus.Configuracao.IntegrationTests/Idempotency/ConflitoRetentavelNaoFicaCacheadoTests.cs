namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Idempotency;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using System.Text.Json;

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
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// <b>O conflito que a resposta declara retentável não fica guardado; o durável fica.</b>
/// </summary>
/// <remarks>
/// <para>
/// A reserva de idempotência guarda toda resposta abaixo de 500 por 24 h. Para o conflito de
/// corrida isso nega a única saída que o status oferece: o cliente que preserva a chave — o que a
/// semântica de idempotência manda fazer — recebe em replay o conflito de ontem. Mas descartar
/// todo 409 é pior: um replay tardio de "código já existe", depois que alguém liberou o código,
/// criaria o registro que o cliente acredita não ter criado.
/// </para>
/// <para>
/// Os testes correm no nível do <b>filtro</b>, e verificam pelo <c>LookupAsync</c> e não pela
/// resposta HTTP, por duas razões. O harness não executa o <c>Result</c>, então a resposta não
/// existe; e o que está sendo afirmado é o estado da reserva, que é o que decide o desfecho da
/// requisição seguinte. Montar a corrida de verdade produziria intermitência sem provar mais.
/// </para>
/// </remarks>
[Collection(ConfiguracaoEndpointCollection.Name)]
[Trait("Category", "Integration")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class ConflitoRetentavelNaoFicaCacheadoTests
{
    private const string Rota =
        "/api/configuracao/admin/termos-consentimento/00000000-0000-0000-0000-000000000001/revisar";

    private const string Endpoint = "POST " + Rota;

    private readonly ConfiguracaoEndpointFixture _fixture;

    public ConflitoRetentavelNaoFicaCacheadoTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "409 declarado retentável libera a reserva — o retry com a mesma chave alcança a action")]
    public async Task Conflito_QuandoRespostaDeclaraRetentavel_DeveLiberarAReserva()
    {
        // O calendário, e não o termo de consentimento: o código do termo é compartilhado com a
        // marcação de revisado, cuja repetição aprovaria texto que ninguém leu, e por isso ele
        // NÃO é retentável. Trocar de exemplo aqui é o teste seguindo a classificação viva, que
        // é o que ele existe para fazer.
        IdempotencyOutcome desfecho = await ExecutarComErroDeDominioAsync(
            CalendarioDiasUteisErrorCodes.ConflitoDeConcorrencia);

        desfecho.Should().Be(
            IdempotencyOutcome.Miss,
            "o conflito de corrida existe para dizer 'releia e tente de novo', e guardá-lo por 24 h nega ao cliente "
            + "exatamente a saída que o status oferece");
    }

    [Fact(DisplayName = "409 sem a declaração continua guardado — é o que impede a mutação autorizada por engano")]
    public async Task Conflito_QuandoRespostaNaoDeclaraRetentavel_DeveContinuarGuardado()
    {
        // O discriminante, e o teste que de fato separa as duas classificações no código novo:
        // 'sigla já existe' descreve o estado do catálogo, não uma corrida. Liberar a reserva
        // faria um replay tardio — depois que alguém liberasse a sigla — CRIAR o registro que o
        // cliente acredita não ter criado. A chave teria autorizado a mutação que ela existe
        // para impedir.
        IdempotencyOutcome desfecho = await ExecutarComErroDeDominioAsync(
            CampusErrorCodes.SiglaJaExiste);

        desfecho.Should().Be(IdempotencyOutcome.HitMatch);
    }

    [Fact(DisplayName = "409 com corpo que não se pode ler continua guardado — a dúvida resolve pelo lado seguro")]
    public async Task Conflito_QuandoCorpoNaoEhLegivel_DeveContinuarGuardado()
    {
        // Errar para 'guardado' custa um retry recusado; errar para o outro lado custa uma
        // mutação indevida. A dúvida resolve pelo primeiro.
        IdempotencyOutcome desfecho = await ExecutarComRespostaAsync(
            StatusCodes.Status409Conflict, "isto não é json");

        desfecho.Should().Be(IdempotencyOutcome.HitMatch);
    }

    [Fact(DisplayName = "A declaração não alcança status que não seja conflito")]
    public async Task Resposta_QuandoNaoEhConflito_DeveContinuarGuardadaMesmoDeclarandoRetentavel()
    {
        // A marca fala de conflito. Um 422 que a carregasse por engano não pode liberar reserva:
        // recusa de conteúdo não vira aceitação por repetição.
        IdempotencyOutcome desfecho = await ExecutarComRespostaAsync(
            StatusCodes.Status422UnprocessableEntity,
            """{"status":422,"code":"uniplus.qualquer","retryable":true}""");

        desfecho.Should().Be(IdempotencyOutcome.HitMatch);
    }

    /// <summary>
    /// Roda o filtro com a resposta que o código de domínio <b>realmente</b> produz.
    /// </summary>
    /// <remarks>
    /// O corpo sai da classificação viva, e não escrito à mão no teste. É o que faz o teste falar
    /// sobre a classificação daquele código, e não apenas sobre o filtro saber ler um campo:
    /// trocar a declaração de um dos dois códigos inverte o resultado, que é a prova de que os
    /// dois testes medem lados opostos da mesma decisão.
    /// </remarks>
    private async Task<IdempotencyOutcome> ExecutarComErroDeDominioAsync(string codigoDeDominio)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        IDomainErrorMapper mapper = scope.ServiceProvider.GetRequiredService<IDomainErrorMapper>();

        ObjectResult resposta = (ObjectResult)Result
            .Failure(new DomainError(codigoDeDominio, "conflito sintético"))
            .ToActionResult(mapper);

        resposta.StatusCode.Should().Be(
            StatusCodes.Status409Conflict,
            $"'{codigoDeDominio}' precisa continuar sendo um conflito para este teste dizer algo");

        return await ExecutarComRespostaAsync(
            resposta.StatusCode!.Value,
            JsonSerializer.Serialize(resposta.Value));
    }

    /// <summary>
    /// Roda o filtro com um <c>next()</c> que escreve a resposta dada e devolve o desfecho do
    /// lookup seguinte, com a mesma chave e o mesmo corpo.
    /// </summary>
    private async Task<IdempotencyOutcome> ExecutarComRespostaAsync(int status, string corpoDaResposta)
    {
        MonolitoApiFactory api = _fixture.Factory;
        string idempotencyKey = Guid.NewGuid().ToString();

        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        IServiceProvider services = scope.ServiceProvider;

        EfCoreIdempotencyStore<ConfiguracaoDbContext> store = ActivatorUtilities
            .CreateInstance<EfCoreIdempotencyStore<ConfiguracaoDbContext>>(services);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.IsAuthenticated.Returns(true);
        userContext.UserId.Returns("conflito-retentavel-test-user");

        IdempotencyFilter<ConfiguracaoDbContext> filter = new(
            store,
            services.GetRequiredService<IUniPlusEncryptionService>(),
            services.GetRequiredService<IDomainErrorMapper>(),
            services.GetRequiredService<TimeProvider>(),
            userContext,
            services.GetRequiredService<IOptions<IdempotencyOptions>>());

        ControllerActionDescriptor actionDescriptor = new()
        {
            MethodInfo = typeof(TermosConsentimentoController)
                .GetMethod(nameof(TermosConsentimentoController.MarcarRevisado))!,
            ControllerTypeInfo = typeof(TermosConsentimentoController).GetTypeInfo(),
        };

        byte[] corpoDaRequisicao = Encoding.UTF8.GetBytes("""{"texto":"corpo de teste"}""");

        DefaultHttpContext httpContext = new() { RequestServices = services };
        httpContext.Request.Method = "POST";
        httpContext.Request.Path = Rota;
        httpContext.Request.Headers["Idempotency-Key"] = idempotencyKey;
        httpContext.Request.Body = new MemoryStream(corpoDaRequisicao);
        httpContext.Request.ContentLength = corpoDaRequisicao.Length;
        httpContext.Response.Body = new MemoryStream();

        ActionContext actionContext = new(httpContext, new RouteData(), actionDescriptor);
        List<IFilterMetadata> filters = [];
        ResourceExecutingContext executingContext = new(actionContext, filters, []);

        await filter.OnResourceExecutionAsync(executingContext, async () =>
        {
            // Neste ponto Response.Body JÁ É o stream de captura que o filtro instalou — é assim
            // que a resposta chega ao bloco de decisão, do mesmo jeito que chegaria numa
            // requisição real.
            httpContext.Response.StatusCode = status;
            await httpContext.Response.Body
                .WriteAsync(Encoding.UTF8.GetBytes(corpoDaResposta), CancellationToken.None);

            return new ResourceExecutedContext(actionContext, filters);
        });

        IdempotencyLookupResult lookup = await store.LookupAsync(
            $"user:{userContext.UserId}", Endpoint, idempotencyKey,
            ComputeBodyHash(corpoDaRequisicao), CancellationToken.None);

        return lookup.Outcome;
    }

    private static string ComputeBodyHash(byte[] bytes)
    {
        Span<byte> hash = stackalloc byte[32];
        System.Security.Cryptography.SHA256.HashData(bytes, hash);
        return Convert.ToHexStringLower(hash);
    }
}
