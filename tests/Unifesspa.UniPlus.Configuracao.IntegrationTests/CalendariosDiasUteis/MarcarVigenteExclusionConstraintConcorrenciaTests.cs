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
/// Investigação empírica da issue #1032: observa, contra Postgres real, o que
/// acontece quando a exclusion constraint <c>ex_calendario_dias_uteis_vigente_unico</c>
/// (<c>DEFERRABLE INITIALLY DEFERRED</c>) é violada por duas ativações concorrentes
/// que não disputam a MESMA linha (portanto não colidem por xmin — cada uma marca
/// um calendário DIFERENTE como vigente, e nenhuma delas tem um "vigente anterior"
/// em comum para demarcar).
/// </summary>
/// <remarks>
/// <para>A corrida é forçada de forma determinística, mesmo padrão de
/// <c>CalendarioDiasUteisConcorrenciaTests</c>: uma transação explícita (<c>txB</c>)
/// faz um UPDATE cru marcando o calendário B como vigente, sem commitar. O handler
/// real (<c>busA</c>) marca o calendário A como vigente — como nenhum registro está
/// vigente no estado COMMITADO, <c>vigenteAnterior</c> é <see langword="null"/> e o
/// handler não toca a linha de B. O <c>SaveChangesAsync</c> do handler grava A
/// normalmente (nenhum conflito de xmin: A e B são linhas diferentes). O handler
/// então força a checagem da constraint DEFERRED via <c>SET CONSTRAINTS ALL
/// IMMEDIATE</c> (<c>ForcarChecagemImediataDeConstraintsAsync</c>) — como B tem uma
/// escrita concorrente ainda não resolvida que também tornaria a constraint
/// potencialmente violada, o Postgres bloqueia essa checagem até <c>txB</c>
/// terminar (mesmo mecanismo de espera por transação concorrente que unique/exclusion
/// constraints usam para não decidir prematuramente sobre uma linha ainda em voo).</para>
/// <para>Ao liberar <c>txB</c> com commit, a checagem de <c>busA</c> é resolvida: as
/// duas linhas (A e B) ficam vigente=true simultaneamente e a constraint estoura
/// dentro do próprio <c>try</c> do handler — mas via <c>Npgsql.PostgresException</c>
/// bruta do <c>ExecuteSqlRawAsync("SET CONSTRAINTS ALL IMMEDIATE")</c>, não de
/// <c>SaveChangesAsync</c>. Esse comando falhando marca a transação Postgres
/// subjacente como ABORTADA — diferente do caso xmin (onde o UPDATE afetando 0
/// linhas não aborta a transação, é só um resultado de rowcount que o EF Core
/// interpreta como <c>DbUpdateConcurrencyException</c>). O teste observa se
/// <c>ChangeTracker.Clear()</c> sozinho basta para o <c>SaveChangesAsync</c>
/// automático do outbox (que roda depois que o handler retorna, ADR-0004) não
/// relançar — ou se a transação abortada faz esse segundo SaveChanges falhar de um
/// jeito diferente (<c>25P02</c>), o que exigiria descartar a transação inteira, não
/// só o rastreamento do EF Core.</para>
/// </remarks>
[Collection(ConfiguracaoEndpointCollection.Name)]
[Trait("Category", "Integration")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class MarcarVigenteExclusionConstraintConcorrenciaTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public MarcarVigenteExclusionConstraintConcorrenciaTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName =
        "Duas ativações concorrentes de calendários DIFERENTES colidem na exclusion constraint DEFERRED, não por xmin")]
    public async Task MarcarVigente_DuasAtivacoesConcorrentesDeCalendariosDiferentes_ColideNaExclusionConstraint()
    {
        MonolitoApiFactory api = _fixture.Factory;

        Guid idA;
        Guid idB;
        await using (AsyncServiceScope setupScope = api.Services.CreateAsyncScope())
        {
            ConfiguracaoDbContext db = setupScope.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();

            // ConfiguracaoEndpointFixture é compartilhado por toda a
            // ConfiguracaoEndpointCollection — outro teste da mesma collection
            // (ex.: CalendarioDiasUteisEndpointTests.MarcarVigente_DesmarcaOAnterior)
            // pode ter deixado um terceiro calendário vigente=true para trás.
            // Sem desmarcá-lo aqui, o handler de A leria ESSE terceiro registro
            // como vigenteAnterior e tentaria demarcá-lo dentro da própria
            // transação — um xmin extra fora do controle deste teste, alheio à
            // corrida A-vs-B que ele quer provar.
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE configuracao.calendario_dias_uteis SET vigente = false WHERE vigente = true");

            CalendarioDiasUteis calendarA = CalendarioDiasUteis.Criar(
                $"exc-a-{Guid.NewGuid():N}"[..20],
                [new DiaNaoUtilCriacao("NACIONAL", null, null, null, new DateOnly(2099, 1, 1), "Ano novo")]).Value!;
            CalendarioDiasUteis calendarB = CalendarioDiasUteis.Criar(
                $"exc-b-{Guid.NewGuid():N}"[..20],
                [new DiaNaoUtilCriacao("NACIONAL", null, null, null, new DateOnly(2099, 1, 1), "Ano novo")]).Value!;
            db.CalendariosDiasUteis.AddRange(calendarA, calendarB);
            await db.SaveChangesAsync();
            idA = calendarA.Id;
            idB = calendarB.Id;
        }

        // txB: marca B como vigente via UPDATE cru, sem commitar — nenhum calendário
        // fica vigente=true no estado COMMITADO enquanto txB está aberta, então o
        // handler de A não vê "vigenteAnterior" nenhum e não toca a linha de B.
        await using AsyncServiceScope scopeB = api.Services.CreateAsyncScope();
        ConfiguracaoDbContext dbB = scopeB.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        await using IDbContextTransaction txB = await dbB.Database.BeginTransactionAsync();
        await dbB.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE configuracao.calendario_dias_uteis SET vigente = true WHERE id = {idB}");

        await using AsyncServiceScope scopeA = api.Services.CreateAsyncScope();
        IMessageBus busA = scopeA.ServiceProvider.GetRequiredService<IMessageBus>();
        Task<Result> taskA = busA.InvokeAsync<Result>(new MarcarVigenteCalendarioDiasUteisCommand(idA));

        // Prova direta de que a checagem forçada (SET CONSTRAINTS ALL IMMEDIATE) do
        // handler de A está bloqueada esperando txB resolver — não uma aposta de
        // tempo. Diferente do teste de xmin, o texto da query bloqueada não contém o
        // nome da tabela (é o comando SET CONSTRAINTS em si), por isso o filtro é por
        // esse texto, não pela tabela.
        await WaitForConstraintCheckToBlockAsync(api, taskA);

        await txB.CommitAsync();

        Result result = await taskA;

        // Registra o resultado observado para orientar a decisão de fix — o valor
        // desta asserção é DOCUMENTAR o comportamento real, não presumi-lo.
        result.IsFailure.Should().BeTrue(
            "as duas linhas (A e B) ficaram vigente=true simultaneamente assim que txB commitou — " +
            "a exclusion constraint deve ter estourado dentro do próprio catch do handler");

        // Comportamento crítico sob investigação: o SaveChangesAsync automático do
        // outbox do Wolverine roda LOGO APÓS o handler retornar, na MESMA
        // transação/DbContext (ADR-0004). Se ChangeTracker.Clear() não bastar porque
        // a transação Postgres ficou ABORTADA pelo "SET CONSTRAINTS ALL IMMEDIATE"
        // que falhou, esse SaveChangesAsync automático relança — e como ele roda FORA
        // de qualquer catch do handler, vira 500 em vez do 409 que o handler já
        // decidiu. `taskA` já awaitou o ciclo completo do Wolverine (incluindo o
        // outbox); chegar aqui sem exceção não capturada é a prova de que o outbox
        // não relançou.
        await using AsyncServiceScope readScope = api.Services.CreateAsyncScope();
        ConfiguracaoDbContext readDb = readScope.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        CalendarioDiasUteis persistedA = await readDb.CalendariosDiasUteis.SingleAsync(c => c.Id == idA);
        CalendarioDiasUteis persistedB = await readDb.CalendariosDiasUteis.SingleAsync(c => c.Id == idB);

        persistedA.Vigente.Should().BeFalse(
            "a transação do handler de A abortou na checagem da constraint — nada dela deveria ter persistido");
        persistedB.Vigente.Should().BeTrue(
            "txB commitou isolada da corrida de A e deve refletir exatamente o que ela escreveu");
    }

    private static async Task WaitForConstraintCheckToBlockAsync(MonolitoApiFactory api, Task taskExpectedToBlock)
    {
        await using AsyncServiceScope pollScope = api.Services.CreateAsyncScope();
        ConfiguracaoDbContext pollDb = pollScope.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();

        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (taskExpectedToBlock.IsCompleted)
            {
                Assert.Fail("A tarefa completou antes de bloquear na checagem da constraint — a corrida não foi forçada como esperado.");
            }

            int blockedBackends = await pollDb.Database
                .SqlQuery<int>(
                    $"""
                    SELECT count(*)::int AS "Value" FROM pg_stat_activity
                    WHERE wait_event_type = 'Lock' AND query ILIKE '%SET CONSTRAINTS%'
                    """)
                .SingleAsync();

            if (blockedBackends > 0)
            {
                return;
            }

            await Task.Delay(20);
        }

        Assert.Fail("Nenhum backend ficou bloqueado na checagem da constraint dentro do prazo — a corrida não foi forçada.");
    }
}
