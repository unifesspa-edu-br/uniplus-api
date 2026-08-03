namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.CalendariosDiasUteis;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Application.Commands.CalendariosDiasUteis;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Results;

using Wolverine;

/// <summary>
/// Prova de ponta a ponta do padrão canônico de concorrência otimista
/// (ADR-0119) para <c>RemoverCalendarioDiasUteisCommandHandler</c>: o handler
/// não captura <c>DbUpdateConcurrencyException</c> — se o
/// <c>SaveChangesAsync</c> automático do outbox (<c>AutoApplyTransactions</c>,
/// ADR-0004) tentasse rodar de novo sobre entidades ainda rastreadas, o lado
/// perdedor da corrida vazaria a exceção fora de qualquer catch em vez de
/// propagá-la limpa uma única vez.
/// </summary>
/// <remarks>
/// <c>Task.WhenAll</c> sem sincronização (o "safety net" de
/// <c>PrecedenciaFaseConcorrenciaTests</c>) não reproduz a corrida de forma
/// confiável aqui — sob a suíte completa, um dos dois lados pode atrasar o
/// bastante para que o outro já tenha commitado antes mesmo da sua própria
/// leitura, produzindo um 404 de negócio em vez de um conflito de xmin
/// (verificado empiricamente: passa isolado, falha sob carga da suíte). Este
/// teste força a corrida de forma determinística: uma transação explícita
/// (<c>ctxB</c>) segura o lock de linha do Postgres depois de já ter lido a
/// entidade; o handler real (<c>busA</c>) começa, lê a mesma linha (ainda sem
/// bloqueio de leitura) e bloqueia só na hora de escrever. Em vez de apostar
/// num prazo fixo, o teste faz poll em <c>pg_locks</c> até observar o backend
/// de <c>busA</c> realmente esperando pelo lock da linha — prova direta de
/// que ele já leu o xmin antigo e chegou à escrita, não uma inferência de
/// tempo. Só então libera <c>ctxB</c> com um UPDATE que já bate o xmin.
/// </remarks>
[Collection(ConfiguracaoEndpointCollection.Name)]
[Trait("Category", "Integration")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class CalendarioDiasUteisConcorrenciaTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public CalendarioDiasUteisConcorrenciaTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName =
        "Remoção colidindo com uma escrita concorrente do mesmo dataset propaga DbUpdateConcurrencyException limpa (sem segunda tentativa do outbox)")]
    public async Task Remocao_ColideComEscritaConcorrente_PropagaExcecaoLimpa()
    {
        MonolitoApiFactory api = _fixture.Factory;

        Guid id;
        await using (AsyncServiceScope setupScope = api.Services.CreateAsyncScope())
        {
            ConfiguracaoDbContext db = setupScope.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
            CalendarioDiasUteis criado = CalendarioDiasUteis.Criar(
                $"conc-{Guid.NewGuid():N}"[..20],
                [new DiaNaoUtilCriacao("NACIONAL", null, new DateOnly(2099, 1, 1), "Ano novo")]).Value!;
            db.CalendariosDiasUteis.Add(criado);
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
            $"UPDATE configuracao.calendario_dias_uteis SET updated_at = now() WHERE id = {id}");

        await using AsyncServiceScope scopeA = api.Services.CreateAsyncScope();
        IMessageBus busA = scopeA.ServiceProvider.GetRequiredService<IMessageBus>();
        Task<Result> taskA = busA.InvokeAsync<Result>(new RemoverCalendarioDiasUteisCommand(id));

        // Prova direta (não uma aposta de tempo) de que busA já leu o xmin
        // antigo e está bloqueado tentando escrever: poll em pg_locks até
        // aparecer um lock NÃO concedido na tabela — só existe se alguém além
        // de txB está esperando a linha.
        await ConcorrenciaTestHelpers.AguardarLockPendenteAsync(api, "calendario_dias_uteis", taskA);

        await txB.CommitAsync();

        Func<Task> act = async () => await taskA;

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "o xmin lido pelo handler ficou obsoleto assim que txB commitou a própria escrita na mesma linha");

        await using AsyncServiceScope readScope = api.Services.CreateAsyncScope();
        ConfiguracaoDbContext readDb = readScope.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        CalendarioDiasUteis persistido = await readDb.CalendariosDiasUteis.SingleAsync(c => c.Id == id);
        persistido.IsDeleted.Should().BeFalse(
            "o handler perdeu a corrida e não deve ter removido nada — nenhuma segunda tentativa do outbox deve ter reaplicado a remoção");
    }
}
