namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Idempotency;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;

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
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        CenarioDeIdempotencia cenario = new(scope.ServiceProvider, "idempotency-filter-exception-test-user");
        ResourceExecutingContext requisicao = cenario.CriarRequisicao();
        await cenario.Filter.OnResourceExecutionAsync(requisicao, () => ExcecaoPendenteAsync(requisicao));
        IdempotencyLookupResult lookup = await cenario.ConsultarReservaAsync();
        lookup.Outcome.Should().Be(
            IdempotencyOutcome.Processing,
            "uma exceção pendente e não tratada não deve deixar entrada Completed (200 fabricado) nem ser apagada — "
            + "apagar arriscaria reexecutar uma mutação que já pode ter persistido antes da exceção");
        await VerificarRepeticaoRecusadaAsync(cenario);
    }

    [Fact(DisplayName =
        "Falha de registrar o acesso libera a reserva: nada foi persistido, e a repetição com a mesma chave executa")]
    public async Task OnResourceExecutionAsync_WithRegistroDeAcessoNaoGravado_ReleasesReservation()
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        CenarioDeIdempotencia cenario = new(scope.ServiceProvider, "idempotency-filter-registro-test-user");
        ResourceExecutingContext requisicao = cenario.CriarRequisicao();
        await cenario.Filter.OnResourceExecutionAsync(requisicao, () => RegistroNaoGravadoAsync(requisicao));
        IdempotencyLookupResult depois = await cenario.ConsultarReservaAsync();
        depois.Outcome.Should().Be(IdempotencyOutcome.Miss, "a chave fica livre para a repetição");
    }

    private static Task<ResourceExecutedContext> ExcecaoPendenteAsync(ResourceExecutingContext requisicao) =>
        Task.FromResult(new ResourceExecutedContext(requisicao, requisicao.Filters)
        {
            Exception = new InvalidOperationException("erro sintético não relacionado a idempotência"),
            ExceptionHandled = false,
        });

    private static Task<ResourceExecutedContext> RegistroNaoGravadoAsync(ResourceExecutingContext requisicao) =>
        Task.FromResult(new ResourceExecutedContext(requisicao, requisicao.Filters)
        {
            Exception = new RegistroDeAcessoNaoGravadoException(new InvalidOperationException("broker fora")),
            ExceptionHandled = false,
        });

    private static async Task VerificarRepeticaoRecusadaAsync(CenarioDeIdempotencia cenario)
    {
        ResourceExecutingContext requisicao = cenario.CriarRequisicao();
        bool nextInvoked = false;
        await cenario.Filter.OnResourceExecutionAsync(requisicao, () =>
        {
            nextInvoked = true;
            return Task.FromResult(new ResourceExecutedContext(requisicao, requisicao.Filters));
        });
        nextInvoked.Should().BeFalse(
            "o retry imediato deve ser rejeitado pelo lookup de Processing, sem nunca chegar a invocar a action de novo");
        requisicao.Result.Should().NotBeNull(
            "o filtro deve ter produzido a resposta 409 ProcessingConflict via short-circuit");
    }
}
