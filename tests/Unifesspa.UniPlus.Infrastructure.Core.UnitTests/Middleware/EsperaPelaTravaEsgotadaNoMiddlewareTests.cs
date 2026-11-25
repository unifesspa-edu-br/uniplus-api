namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Middleware;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;

using Npgsql;

using Unifesspa.UniPlus.Infrastructure.Core.Middleware;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Apoio;
using Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Apoio.ControllersDeTeste;

/// <summary>
/// A espera esgotada pela trava de uma linha chega ao cliente como indisponibilidade temporária, sem prazo de nova
/// tentativa: a resposta manda reler o estado e enviar de novo, com uma nova chave onde a rota exige a
/// <c>Idempotency-Key</c>. O erro do banco que não passou pela conversão da trava segue como erro interno.
/// </summary>
public sealed class EsperaPelaTravaEsgotadaNoMiddlewareTests
{
    [Theory(DisplayName = "Com Idempotency-Key exigida, responde 503 sem Retry-After, sem vazar a causa, e manda usar uma nova chave")]
    [InlineData(nameof(RotaDeTravaDeTesteController.ComChave))]
    [InlineData(nameof(RotaDeTravaDeTesteController.SemDeclaracaoComChave))]
    public async Task InvokeAsync_RotaComChave_MandaUsarNovaChave(string acao)
    {
        string detalhe = await Responde503SemRetryAfterAsync(acao);

        detalhe.Should().Be(
            "Outra operação sobre este processo está em andamento. Releia o estado do processo e envie novamente com uma nova chave de idempotência.");
    }

    [Theory(DisplayName = "Sem Idempotency-Key, responde 503 sem Retry-After e manda reler o estado, sem falar em chave")]
    [InlineData(nameof(RotaDeTravaDeTesteController.SemChave))]
    [InlineData(nameof(RotaDeTravaDeTesteController.SemDeclaracao))]
    public async Task InvokeAsync_RotaSemChave_MandaReenviarSemChave(string acao)
    {
        string detalhe = await Responde503SemRetryAfterAsync(acao);

        detalhe.Should().Be("Outra operação sobre este processo está em andamento. Releia o estado do processo e envie novamente.");
    }

    [Fact(DisplayName = "Sem rota resolvida, a espera esgotada não é tratada como repetição segura")]
    public async Task InvokeAsync_SemRotaResolvida_Responde503SemRetryAfter()
    {
        DefaultHttpContext context = MiddlewareDeErrosDeTeste.CriarContexto();
        GlobalExceptionMiddleware middleware = MiddlewareDeErrosDeTeste.Criar(_ => throw EsperaEsgotada("causa"));

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        context.Response.Headers.ContainsKey("Retry-After").Should().BeFalse();
    }

    [Fact(DisplayName = "O erro 55P03 do banco fora da trava do processo não é convertido: segue como erro interno, sem Retry-After")]
    public async Task InvokeAsync_LockNotAvailableSemConversao_Responde500()
    {
        DefaultHttpContext context = ContextoDaRota(nameof(RotaDeTravaDeTesteController.SemChave));
        PostgresException crua = new("ERROR", "ERROR", "canceling statement due to lock timeout", PostgresErrorCodes.LockNotAvailable);
        GlobalExceptionMiddleware middleware = MiddlewareDeErrosDeTeste.Criar(_ => throw crua);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        context.Response.Headers.ContainsKey("Retry-After").Should().BeFalse();
        using JsonDocument doc = await MiddlewareDeErrosDeTeste.LerBodyAsync(context);
        doc.RootElement.GetProperty("code").GetString().Should().Be("uniplus.internal.unexpected");
    }

    private static async Task<string> Responde503SemRetryAfterAsync(string acao)
    {
        DefaultHttpContext context = ContextoDaRota(acao);
        GlobalExceptionMiddleware middleware = MiddlewareDeErrosDeTeste.Criar(_ => throw EsperaEsgotada("valor sigiloso do banco"));

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        context.Response.Headers.ContainsKey("Retry-After").Should().BeFalse();
        using JsonDocument doc = await MiddlewareDeErrosDeTeste.LerBodyAsync(context);
        doc.RootElement.GetProperty("code").GetString().Should().Be("uniplus.concorrencia.espera_esgotada");
        doc.RootElement.GetProperty("title").GetString().Should().Be("Processo ocupado por outra operação");
        doc.RootElement.GetRawText().Should().NotContain("valor sigiloso do banco");
        return doc.RootElement.GetProperty("detail").GetString()!;
    }

    private static EsperaPelaTravaEsgotadaException EsperaEsgotada(string causa) =>
        new(new InvalidOperationException(causa));

    private static DefaultHttpContext ContextoDaRota(string acao)
    {
        DefaultHttpContext context = MiddlewareDeErrosDeTeste.CriarContexto();
        using AcoesDoMvcDeTeste mvc = new(typeof(RotaDeTravaDeTesteController));
        ControllerActionDescriptor descritor = mvc.Acao(typeof(RotaDeTravaDeTesteController), acao);
        context.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection([.. descritor.EndpointMetadata, descritor]), acao));
        return context;
    }

}
