namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Idempotency;

using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
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
        ObjectResult resposta = await RespostaDeDominioAsync(
            CalendarioDiasUteisErrorCodes.ConflitoDeConcorrencia);
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        JsonSerializerOptions opcoesDoHost = scope.ServiceProvider
            .GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions;
        using JsonDocument corpo = JsonDocument.Parse(JsonSerializer.Serialize(resposta.Value, opcoesDoHost));
        VerificarDeclaracaoRetentavel(corpo);
    }

    private static void VerificarDeclaracaoRetentavel(JsonDocument corpo)
    {
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
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        CenarioDeIdempotencia cenario = new(scope.ServiceProvider, "conflito-retentavel-test-user");
        ResourceExecutingContext requisicao = cenario.CriarRequisicao();
        RespostaDoMvc resposta = new(status, corpoDaResposta, resultadoTipado, excecaoPendente);
        await cenario.Filter.OnResourceExecutionAsync(requisicao, () => resposta.ExecutarAsync(requisicao));
        return (await cenario.ConsultarReservaAsync()).Outcome;
    }

    private sealed record RespostaDoMvc(int Status, string Corpo, IActionResult? Resultado, Exception? Excecao)
    {
        public async Task<ResourceExecutedContext> ExecutarAsync(ResourceExecutingContext requisicao)
        {
            requisicao.HttpContext.Response.StatusCode = Status;
            await requisicao.HttpContext.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(Corpo), CancellationToken.None);
            return new ResourceExecutedContext(requisicao, requisicao.Filters)
            {
                Result = Resultado,
                Exception = Excecao,
            };
        }
    }
}
