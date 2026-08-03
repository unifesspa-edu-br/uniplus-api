namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Application.Commands.CalendariosDiasUteis;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Results;

using Xunit.Sdk;

using IMessageBus = Wolverine.IMessageBus;

/// <summary>
/// Prova de correção da issue #1031: <see cref="ConcorrenciaTestHelpers.AguardarBackendBloqueadoAsync"/>
/// identifica o backend bloqueado correlacionando via <c>pg_blocking_pids</c> —
/// imune tanto a texto de query coincidente quanto a qualquer OUTRO backend
/// bloqueado por um lock não relacionado ao que o chamador está segurando (o
/// achado P2 do Codex sobre a primeira versão deste teste, que só excluía PIDs
/// conhecidos sem confirmar QUEM realmente bloqueava o candidato).
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
[Trait("Category", "Integration")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class ConcorrenciaTestHelpersTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public ConcorrenciaTestHelpersTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName =
        "Um backend bloqueado por um lock alheio (mesma tabela, holder diferente) não é confundido com o backend real sob teste")]
    public async Task AguardarBackendBloqueadoAsync_ComBackendBloqueadoPorHolderAlheio_NaoDaFalsoPositivo()
    {
        MonolitoApiFactory api = _fixture.Factory;

        Guid idAlvo;
        Guid idDecoy;
        await using (AsyncServiceScope setupScope = api.Services.CreateAsyncScope())
        {
            ConfiguracaoDbContext db = setupScope.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
            CalendarioDiasUteis alvo = CalendarioDiasUteis.Criar(
                $"pid-alvo-{Guid.NewGuid():N}"[..20],
                [new DiaNaoUtilCriacao("NACIONAL", null, new DateOnly(2099, 1, 1), "x")]).Value!;
            CalendarioDiasUteis decoy = CalendarioDiasUteis.Criar(
                $"pid-decoy-{Guid.NewGuid():N}"[..20],
                [new DiaNaoUtilCriacao("NACIONAL", null, new DateOnly(2099, 1, 1), "x")]).Value!;
            db.CalendariosDiasUteis.AddRange(alvo, decoy);
            await db.SaveChangesAsync();
            idAlvo = alvo.Id;
            idDecoy = decoy.Id;
        }

        // Decoy: uma segunda corrida, TOTALMENTE alheia à corrida sob teste,
        // sobre uma linha DIFERENTE da mesma tabela — um backend genuinamente
        // bloqueado (wait_event_type='Lock'), com query mencionando
        // "calendario_dias_uteis" por coincidência, mas bloqueado por um
        // holder que NÃO é o que a corrida real vai usar.
        await using AsyncServiceScope scopeDecoyHolder = api.Services.CreateAsyncScope();
        ConfiguracaoDbContext dbDecoyHolder = scopeDecoyHolder.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        await using IDbContextTransaction txDecoyHolder = await dbDecoyHolder.Database.BeginTransactionAsync();
        await dbDecoyHolder.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE configuracao.calendario_dias_uteis SET updated_at = now() WHERE id = {idDecoy}");
        int pidDecoyHolder = await ConcorrenciaTestHelpers.ObterPidDaConexaoAsync(dbDecoyHolder);

        await using AsyncServiceScope scopeDecoyBlocked = api.Services.CreateAsyncScope();
        ConfiguracaoDbContext dbDecoyBlocked = scopeDecoyBlocked.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        await using IDbContextTransaction txDecoyBlocked = await dbDecoyBlocked.Database.BeginTransactionAsync();
        Task decoyBlockedTask = dbDecoyBlocked.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE configuracao.calendario_dias_uteis SET updated_at = now() WHERE id = {idDecoy}");

        // Se qualquer asserção abaixo — incluindo a própria confirmação de que
        // o decoy está bloqueado — falhar antes de liberar txDecoyHolder,
        // decoyBlockedTask continua bloqueado quando o `await using` de
        // txDecoyBlocked/scopeDecoyBlocked começar a descartar a conexão —
        // Npgsql não aceita descartar uma conexão com um comando ainda em
        // voo. O finally garante que txDecoyHolder é liberado (destravando
        // decoyBlockedTask) ANTES de qualquer descarte, seja qual for o
        // caminho de saída (achado do Codex no PR #1036 — a 1ª versão deste
        // fix ainda deixava a confirmação do decoy fora da proteção).
        bool decoyReleased = false;
        try
        {
            // Prova de que o decoy está genuinamente bloqueado — pelo holder
            // DELE, não pelo que a corrida real vai usar.
            await ConcorrenciaTestHelpers.AguardarBackendBloqueadoAsync(
                api, decoyBlockedTask, [pidDecoyHolder], TimeSpan.FromSeconds(5));

            // --- Fase 1: com o decoy já bloqueado, a corrida real (busA) nem
            // começou. Pedir a espera correlacionada ao holder de A (que só vai
            // existir de verdade na Fase 2) não deve ser satisfeita pelo decoy —
            // ele está bloqueado por pidDecoyHolder, não por pidAlvoHolder.
            await using AsyncServiceScope scopeAlvoHolder = api.Services.CreateAsyncScope();
            ConfiguracaoDbContext dbAlvoHolder = scopeAlvoHolder.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
            await using IDbContextTransaction txAlvoHolder = await dbAlvoHolder.Database.BeginTransactionAsync();
            await dbAlvoHolder.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE configuracao.calendario_dias_uteis SET updated_at = now() WHERE id = {idAlvo}");
            int pidAlvoHolder = await ConcorrenciaTestHelpers.ObterPidDaConexaoAsync(dbAlvoHolder);

            Task tarefaQueNuncaBloqueiaDeVerdade = Task.Delay(TimeSpan.FromMinutes(1));
            Func<Task> aguardarSemBusA = () => ConcorrenciaTestHelpers.AguardarBackendBloqueadoAsync(
                api, tarefaQueNuncaBloqueiaDeVerdade, [pidAlvoHolder], TimeSpan.FromMilliseconds(300));

            await aguardarSemBusA.Should().ThrowAsync<FailException>(
                "o decoy está bloqueado por outro holder — pg_blocking_pids não o correlaciona com pidAlvoHolder");

            // --- Fase 2: agora a corrida real (busA) começa e disputa a MESMA
            // linha que dbAlvoHolder está segurando — pg_blocking_pids vai
            // confirmar que o bloqueador de busA é especificamente pidAlvoHolder.
            await using AsyncServiceScope scopeA = api.Services.CreateAsyncScope();
            IMessageBus busA = scopeA.ServiceProvider.GetRequiredService<IMessageBus>();
            Task<Result> taskA = busA.InvokeAsync<Result>(new RemoverCalendarioDiasUteisCommand(idAlvo));

            await ConcorrenciaTestHelpers.AguardarBackendBloqueadoAsync(
                api, taskA, [pidAlvoHolder], TimeSpan.FromSeconds(5));

            await txAlvoHolder.CommitAsync();
            await txDecoyHolder.CommitAsync();
            decoyReleased = true;

            Func<Task> act = async () => await taskA;
            await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
                "o xmin lido por busA ficou obsoleto assim que dbAlvoHolder commitou a própria escrita na mesma linha (ADR-0119 — RemoverCalendarioDiasUteisCommandHandler propaga sem catch)");

            await decoyBlockedTask;
            await txDecoyBlocked.RollbackAsync();
        }
        finally
        {
            if (!decoyReleased)
            {
                await txDecoyHolder.RollbackAsync();
                await decoyBlockedTask;
                await txDecoyBlocked.RollbackAsync();
            }
        }
    }
}
