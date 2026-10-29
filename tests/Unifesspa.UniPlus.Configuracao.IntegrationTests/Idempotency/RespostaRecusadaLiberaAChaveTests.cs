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
/// <b>A resposta 4xx produzida dentro do MVC libera a chave; a que sai por exceção a retém.</b>
/// </summary>
/// <remarks>
/// Os testes correm no nível do <b>filtro</b> e verificam pelo <c>LookupAsync</c>, não pela
/// resposta HTTP: o que se afirma é o estado da reserva, que decide o desfecho da requisição
/// seguinte. O cenário ponta a ponta (403, concessão, 201 com a mesma chave) está em
/// <c>CriacaoDoProcessoLiberaAChaveEndpointTests</c>.
/// </remarks>
[Collection(ConfiguracaoEndpointCollection.Name)]
[Trait("Category", "Integration")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class RespostaRecusadaLiberaAChaveTests
{
    private const string Rota =
        "/api/configuracao/admin/termos-consentimento/00000000-0000-0000-0000-000000000001/revisar";

    private const string Endpoint = "POST " + Rota;

    private const string CorpoSemADeclaracao = """{"status":409,"code":"uniplus.qualquer"}""";


    private readonly ConfiguracaoEndpointFixture _fixture;

    public RespostaRecusadaLiberaAChaveTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory(DisplayName = "4xx produzido dentro do MVC libera a reserva — o retry com a mesma chave alcança a action")]
    [InlineData(StatusCodes.Status401Unauthorized)]
    [InlineData(StatusCodes.Status403Forbidden)]
    [InlineData(StatusCodes.Status406NotAcceptable)]
    [InlineData(StatusCodes.Status409Conflict)]
    public async Task Resposta4xx_DentroDoMvc_DeveLiberarAReserva(int status)
    {
        IdempotencyOutcome desfecho = await ExecutarComRespostaAsync(status, CorpoSemADeclaracao);

        desfecho.Should().Be(
            IdempotencyOutcome.Miss,
            "o 4xx recusa a requisição sem registrar mutação, e guardá-lo prende o cliente que corrige a causa");
    }

    [Theory(DisplayName = "409 de concorrência traduzido pelo handler libera a reserva, declarado retentável ou não")]
    [InlineData(CalendarioDiasUteisErrorCodes.ConflitoDeConcorrencia)]
    [InlineData(CampusErrorCodes.SiglaJaExiste)]
    public async Task Conflito_TraduzidoPeloHandler_DeveLiberarAReserva(string codigoDeDominio)
    {
        ObjectResult resposta = await RespostaDeDominioAsync(codigoDeDominio);

        IdempotencyOutcome desfecho = await ExecutarComRespostaAsync(
            resposta.StatusCode!.Value,
            JsonSerializer.Serialize(resposta.Value),
            resposta);

        desfecho.Should().Be(IdempotencyOutcome.Miss);
    }

    [Fact(DisplayName = "409 propagado como exceção ao tratador global continua retendo a reserva")]
    public async Task Conflito_PropagadoComoExcecao_DeveReterAReserva()
    {
        IdempotencyOutcome desfecho = await ExecutarComRespostaAsync(
            StatusCodes.Status409Conflict,
            CorpoSemADeclaracao,
            excecaoPendente: new InvalidOperationException("conflito sintético propagado"));

        desfecho.Should().Be(
            IdempotencyOutcome.Processing,
            "a exceção pode ter surgido depois de a mutação ser gravada; o caminho por exceção é tratado à parte");
    }

    [Fact(DisplayName = "A declaração sobrevive à serialização que o host configura")]
    public async Task Declaracao_AoSerializarComAsOpcoesDoHost_DeveSairNaRaizDoCorpo()
    {
        // A declaração é contrato público lido pelo cliente, e a página do catálogo manda tratar
        // a ausência como "não conte com repetir". Uma política de nome aplicada às chaves do
        // dicionário de extensões, ou um invólucro novo, renomeariam o campo no corpo sem que
        // nada mais notasse, e o cliente deixaria de retentar.
        ObjectResult resposta = await RespostaDeDominioAsync(
            CalendarioDiasUteisErrorCodes.ConflitoDeConcorrencia);

        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        JsonSerializerOptions opcoesDoHost = scope.ServiceProvider
            .GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions;

        using JsonDocument corpo = JsonDocument.Parse(
            JsonSerializer.Serialize(resposta.Value, opcoesDoHost));

        corpo.RootElement.TryGetProperty(ResultExtensions.RetryableExtensionName, out JsonElement declarado)
            .Should().BeTrue("é por este nome, na raiz, que o cliente lê a declaração");
        declarado.ValueKind.Should().Be(
            JsonValueKind.True,
            "o cliente confere um booleano verdadeiro, não a mera presença da chave");
    }

    /// <summary>
    /// Monta a resposta que o código de domínio realmente produz para um código de erro, e
    /// confirma que ele continua sendo um conflito.
    /// </summary>
    private async Task<ObjectResult> RespostaDeDominioAsync(string codigoDeDominio)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        IDomainErrorMapper mapper = scope.ServiceProvider.GetRequiredService<IDomainErrorMapper>();

        ObjectResult resposta = (ObjectResult)Result
            .Failure(new DomainError(codigoDeDominio, "conflito sintético"))
            .ToActionResult(mapper);

        resposta.StatusCode.Should().Be(
            StatusCodes.Status409Conflict,
            $"'{codigoDeDominio}' precisa continuar sendo um conflito para este teste dizer algo");

        return resposta;
    }

    /// <summary>
    /// Roda o filtro com um <c>next()</c> que escreve a resposta dada e devolve o desfecho do
    /// lookup seguinte, com a mesma chave e o mesmo corpo.
    /// </summary>
    private async Task<IdempotencyOutcome> ExecutarComRespostaAsync(
        int status,
        string corpoDaResposta,
        IActionResult? resultadoTipado = null,
        Exception? excecaoPendente = null)
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

            // O pipeline real popula o Result com o IActionResult que a action devolveu; o
            // harness o reproduz quando o teste quer exercitar esse caminho.
            return new ResourceExecutedContext(actionContext, filters)
            {
                Result = resultadoTipado,
                Exception = excecaoPendente,
            };
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
