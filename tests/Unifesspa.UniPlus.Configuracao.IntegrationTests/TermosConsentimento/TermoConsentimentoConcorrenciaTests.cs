namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TermosConsentimento;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Application.Commands.TermosConsentimento;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Results;

using Wolverine;

/// <summary>
/// Prova de ponta a ponta do padrão canônico de concorrência otimista
/// (ADR-0119) para <c>RemoverTermoConsentimentoCommandHandler</c>: o handler
/// não captura <c>DbUpdateConcurrencyException</c> — se o
/// <c>SaveChangesAsync</c> automático do outbox (<c>AutoApplyTransactions</c>,
/// ADR-0004) tentasse rodar de novo sobre entidades ainda rastreadas, o lado
/// perdedor da corrida vazaria a exceção fora de qualquer catch em vez de
/// propagá-la limpa uma única vez.
/// </summary>
/// <remarks>
/// Mesma técnica determinística de <c>CalendarioDiasUteisConcorrenciaTests</c>
/// (lock de linha explícito via transação segurada) — <c>Task.WhenAll</c> sem
/// sincronização não reproduz a corrida de forma confiável sob carga da suíte
/// completa (verificado empiricamente).
/// </remarks>
[Collection(ConfiguracaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class TermoConsentimentoConcorrenciaTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public TermoConsentimentoConcorrenciaTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName =
        "Remoção colidindo com uma escrita concorrente do mesmo termo propaga DbUpdateConcurrencyException limpa (sem segunda tentativa do outbox)")]
    public async Task Remocao_ColideComEscritaConcorrente_PropagaExcecaoLimpa()
    {
        MonolitoApiFactory api = _fixture.Factory;

        Guid id;
        await using (AsyncServiceScope setupScope = api.Services.CreateAsyncScope())
        {
            ConfiguracaoDbContext db = setupScope.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
            TermoConsentimento criado = TermoConsentimento.Criar("Termo LGPD", null, null, null).Value!;
            db.TermosConsentimento.Add(criado);
            await db.SaveChangesAsync();
            id = criado.Id;
        }

        await using AsyncServiceScope scopeB = api.Services.CreateAsyncScope();
        ConfiguracaoDbContext dbB = scopeB.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        await using IDbContextTransaction txB = await dbB.Database.BeginTransactionAsync();

        // Trava a linha (lock de escrita do Postgres) sem commitar ainda —
        // qualquer outra transação que tente escrever na mesma linha bloqueia
        // aqui até txB liberar.
        await dbB.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE configuracao.termo_consentimento SET updated_at = now() WHERE id = {id}");

        await using AsyncServiceScope scopeA = api.Services.CreateAsyncScope();
        IMessageBus busA = scopeA.ServiceProvider.GetRequiredService<IMessageBus>();
        Task<Result> taskA = busA.InvokeAsync<Result>(new RemoverTermoConsentimentoCommand(id));

        // O handler real (busA) lê a linha normalmente (ainda não bloqueada
        // para leitura) e só bloqueia ao tentar escrever — este WhenAny só prova
        // que taskA não TERMINOU dentro do prazo, não que já chegou ao lock; se
        // taskA ainda não tivesse nem lido a linha quando txB commita, ela leria
        // o xmin já atualizado e sucederia sem conflito — o que faria a asserção
        // final (DbUpdateConcurrencyException) falhar de forma visível, não
        // passar silenciosamente errada. O prazo generoso (bem acima do
        // round-trip local ao Postgres) é para não tornar o teste sensível a
        // variação normal de agendamento, mesmo assim.
        Task tarefaConcluidaCedo = await Task.WhenAny(taskA, Task.Delay(TimeSpan.FromSeconds(1)));
        tarefaConcluidaCedo.Should().NotBe(taskA, "o handler deveria estar bloqueado esperando o lock de txB");

        await txB.CommitAsync();

        Exception? excecaoObservada = null;
        try
        {
            await taskA;
        }
        catch (Exception ex)
        {
            excecaoObservada = ex;
        }

        excecaoObservada.Should().BeOfType<DbUpdateConcurrencyException>(
            "o xmin lido pelo handler ficou obsoleto assim que txB commitou a própria escrita na mesma linha");

        await using AsyncServiceScope readScope = api.Services.CreateAsyncScope();
        ConfiguracaoDbContext readDb = readScope.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        TermoConsentimento persistido = await readDb.TermosConsentimento.SingleAsync(t => t.Id == id);
        persistido.IsDeleted.Should().BeFalse(
            "o handler perdeu a corrida e não deve ter removido nada — nenhuma segunda tentativa do outbox deve ter reaplicado a remoção");
    }
}
