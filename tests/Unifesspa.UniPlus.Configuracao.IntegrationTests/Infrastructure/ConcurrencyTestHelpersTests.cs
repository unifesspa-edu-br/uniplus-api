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
/// Prova de correção da issue #1031: <see cref="ConcorrenciaTestHelpers.WaitForBlockedBackendAsync"/>
/// identifica o backend bloqueado correlacionando via <c>pg_blocking_pids</c> —
/// imune tanto a texto de query coincidente quanto a qualquer OUTRO backend
/// bloqueado por um lock não relacionado ao que o chamador está segurando. A
/// primeira versão deste teste só excluía PIDs conhecidos, sem confirmar QUEM
/// realmente bloqueava o candidato.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
[Trait("Category", "Integration")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class ConcurrencyTestHelpersTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public ConcurrencyTestHelpersTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName =
        "Um backend bloqueado por um lock alheio (mesma tabela, holder diferente) não é confundido com o backend real sob teste")]
    public async Task WaitForBlockedBackendAsync_WithBackendBlockedByUnrelatedHolder_DoesNotFalsePositive()
    {
        MonolitoApiFactory api = _fixture.Factory;

        Guid targetId;
        Guid decoyId;
        await using (AsyncServiceScope setupScope = api.Services.CreateAsyncScope())
        {
            ConfiguracaoDbContext db = setupScope.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
            CalendarioDiasUteis target = CalendarioDiasUteis.Criar(
                $"pid-target-{Guid.NewGuid():N}"[..20],
                [new DiaNaoUtilCriacao("NACIONAL", null, null, null, new DateOnly(2099, 1, 1), "x")]).Value!;
            CalendarioDiasUteis decoy = CalendarioDiasUteis.Criar(
                $"pid-decoy-{Guid.NewGuid():N}"[..20],
                [new DiaNaoUtilCriacao("NACIONAL", null, null, null, new DateOnly(2099, 1, 1), "x")]).Value!;
            db.CalendariosDiasUteis.AddRange(target, decoy);
            await db.SaveChangesAsync();
            targetId = target.Id;
            decoyId = decoy.Id;
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
            $"UPDATE configuracao.calendario_dias_uteis SET updated_at = now() WHERE id = {decoyId}");
        int pdecoyIdHolder = await ConcorrenciaTestHelpers.GetConnectionPidAsync(dbDecoyHolder);

        await using AsyncServiceScope scopeDecoyBlocked = api.Services.CreateAsyncScope();
        ConfiguracaoDbContext dbDecoyBlocked = scopeDecoyBlocked.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        await using IDbContextTransaction txDecoyBlocked = await dbDecoyBlocked.Database.BeginTransactionAsync();
        Task decoyBlockedTask = dbDecoyBlocked.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE configuracao.calendario_dias_uteis SET updated_at = now() WHERE id = {decoyId}");

        // Se qualquer asserção abaixo — incluindo a própria confirmação de que
        // o decoy está bloqueado — falhar antes de liberar txDecoyHolder,
        // decoyBlockedTask continua bloqueado quando o `await using` de
        // txDecoyBlocked/scopeDecoyBlocked começar a descartar a conexão —
        // Npgsql não aceita descartar uma conexão com um comando ainda em
        // voo. Dois flags distintos:
        // "o holder commitou" (não pode mais ser revertido por rollback) é
        // diferente de "o decoy foi de fato aguardado e descartado" — commitar
        // txDecoyHolder libera o lock do LADO DO SERVIDOR, mas não garante que
        // o comando Npgsql bloqueado do LADO DO CLIENTE já retornou; se a
        // asserção final (linha ~135) falhar depois do commit mas antes do
        // await de decoyBlockedTask, o finally ainda precisa aguardá-lo e
        // descartar txDecoyBlocked antes do `await using` de disposal.
        bool decoyHolderCommitted = false;
        bool decoyBlockedCleanedUp = false;
        try
        {
            // Prova de que o decoy está genuinamente bloqueado — pelo holder
            // DELE, não pelo que a corrida real vai usar.
            await ConcorrenciaTestHelpers.WaitForBlockedBackendAsync(
                api, decoyBlockedTask, [pdecoyIdHolder], TimeSpan.FromSeconds(5));

            // --- Fase 1: com o decoy já bloqueado, a corrida real (busA) nem
            // começou. Pedir a espera correlacionada ao holder de A (que só vai
            // existir de verdade na Fase 2) não deve ser satisfeita pelo decoy —
            // ele está bloqueado por pdecoyIdHolder, não por targetHolderPid.
            await using AsyncServiceScope scopeTargetHolder = api.Services.CreateAsyncScope();
            ConfiguracaoDbContext dbTargetHolder = scopeTargetHolder.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
            await using IDbContextTransaction txTargetHolder = await dbTargetHolder.Database.BeginTransactionAsync();
            await dbTargetHolder.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE configuracao.calendario_dias_uteis SET updated_at = now() WHERE id = {targetId}");
            int targetHolderPid = await ConcorrenciaTestHelpers.GetConnectionPidAsync(dbTargetHolder);

            Task taskThatNeverActuallyBlocks = Task.Delay(TimeSpan.FromMinutes(1));
            Func<Task> waitWithoutBusA = () => ConcorrenciaTestHelpers.WaitForBlockedBackendAsync(
                api, taskThatNeverActuallyBlocks, [targetHolderPid], TimeSpan.FromMilliseconds(300));

            await waitWithoutBusA.Should().ThrowAsync<FailException>(
                "o decoy está bloqueado por outro holder — pg_blocking_pids não o correlaciona com targetHolderPid");

            // --- Fase 2: agora a corrida real (busA) começa e disputa a MESMA
            // linha que dbTargetHolder está segurando — pg_blocking_pids vai
            // confirmar que o bloqueador de busA é especificamente targetHolderPid.
            await using AsyncServiceScope scopeA = api.Services.CreateAsyncScope();
            IMessageBus busA = scopeA.ServiceProvider.GetRequiredService<IMessageBus>();
            Task<Result> taskA = busA.InvokeAsync<Result>(new RemoverCalendarioDiasUteisCommand(targetId));

            await ConcorrenciaTestHelpers.WaitForBlockedBackendAsync(
                api, taskA, [targetHolderPid], TimeSpan.FromSeconds(5));

            await txTargetHolder.CommitAsync();
            await txDecoyHolder.CommitAsync();
            decoyHolderCommitted = true;

            Func<Task> act = async () => await taskA;
            await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
                "o xmin lido por busA ficou obsoleto assim que dbTargetHolder commitou a própria escrita na mesma linha (ADR-0119 — RemoverCalendarioDiasUteisCommandHandler propaga sem catch)");

            await decoyBlockedTask;
            await txDecoyBlocked.RollbackAsync();
            decoyBlockedCleanedUp = true;
        }
        finally
        {
            // txDecoyHolder só aceita Rollback se ainda não foi commitado.
            if (!decoyHolderCommitted)
            {
                await txDecoyHolder.RollbackAsync();
            }

            // decoyBlockedTask só é seguro aguardar/descartar depois que o
            // lock que o prendia foi liberado — seja pelo commit acima, seja
            // pelo rollback que acabou de rodar.
            if (!decoyBlockedCleanedUp)
            {
                await decoyBlockedTask;
                await txDecoyBlocked.RollbackAsync();
            }
        }
    }
}
