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
/// identifica o backend bloqueado por exclusão de PID, não por texto de query —
/// imune a uma query concorrente sem relação com a corrida sob teste que
/// mencione a mesma tabela por coincidência, DESDE QUE o chamador enumere essa
/// conexão entre as conhecidas (o antigo <c>ILIKE '%tabela%'</c> não tinha
/// como diferenciar as duas).
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
        "Uma conexão conhecida bloqueada por coincidência de texto não é confundida com o backend real sob teste")]
    public async Task AguardarBackendBloqueadoAsync_ComConexaoConhecidaBloqueadaPorTextoCoincidente_NaoDaFalsoPositivo()
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
        // sobre uma linha DIFERENTE da mesma tabela — sua query de bloqueio
        // menciona "calendario_dias_uteis" por coincidência, exatamente o
        // cenário que o antigo casamento por ILIKE não sabia distinguir.
        await using AsyncServiceScope scopeDecoyHolder = api.Services.CreateAsyncScope();
        ConfiguracaoDbContext dbDecoyHolder = scopeDecoyHolder.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        await using IDbContextTransaction txDecoyHolder = await dbDecoyHolder.Database.BeginTransactionAsync();
        await dbDecoyHolder.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE configuracao.calendario_dias_uteis SET updated_at = now() WHERE id = {idDecoy}");
        int pidDecoyHolder = await ConcorrenciaTestHelpers.ObterPidDaConexaoAsync(dbDecoyHolder);

        await using AsyncServiceScope scopeDecoyBlocked = api.Services.CreateAsyncScope();
        ConfiguracaoDbContext dbDecoyBlocked = scopeDecoyBlocked.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        await using IDbContextTransaction txDecoyBlocked = await dbDecoyBlocked.Database.BeginTransactionAsync();

        // PID capturado ANTES de disparar o comando que vai bloquear — depois
        // disso a conexão fica ocupada e não aceitaria mais nenhum comando
        // concorrente (nem SELECT pg_backend_pid()).
        int pidDecoyBlocked = await ConcorrenciaTestHelpers.ObterPidDaConexaoAsync(dbDecoyBlocked);
        Task decoyBlockedTask = dbDecoyBlocked.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE configuracao.calendario_dias_uteis SET updated_at = now() WHERE id = {idDecoy}");

        await ConcorrenciaTestHelpers.AguardarBackendBloqueadoAsync(
            api, decoyBlockedTask, [pidDecoyHolder], TimeSpan.FromSeconds(5));

        // --- Fase 1: com o decoy já bloqueado (e ainda não excluído da
        // checagem), a corrida real (busA) nem começou — nada de genuinamente
        // novo está bloqueado. Se a identificação ainda fosse por texto, o
        // decoy sozinho já teria satisfeito a espera; com PID, só timeout.
        await using AsyncServiceScope scopeAlvoHolder = api.Services.CreateAsyncScope();
        ConfiguracaoDbContext dbAlvoHolder = scopeAlvoHolder.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        await using IDbContextTransaction txAlvoHolder = await dbAlvoHolder.Database.BeginTransactionAsync();
        await dbAlvoHolder.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE configuracao.calendario_dias_uteis SET updated_at = now() WHERE id = {idAlvo}");
        int pidAlvoHolder = await ConcorrenciaTestHelpers.ObterPidDaConexaoAsync(dbAlvoHolder);

        int[] pidsConhecidos = [pidDecoyHolder, pidDecoyBlocked, pidAlvoHolder];
        Task tarefaQueNuncaBloqueiaDeVerdade = Task.Delay(TimeSpan.FromMinutes(1));
        Func<Task> aguardarSemBusA = () => ConcorrenciaTestHelpers.AguardarBackendBloqueadoAsync(
            api, tarefaQueNuncaBloqueiaDeVerdade, pidsConhecidos, TimeSpan.FromMilliseconds(300));

        await aguardarSemBusA.Should().ThrowAsync<FailException>(
            "o decoy e o holder são conhecidos e excluídos — nenhum backend genuinamente novo está bloqueado ainda");

        // --- Fase 2: agora a corrida real (busA) começa e disputa a MESMA
        // linha que dbAlvoHolder está segurando — seu PID não está na lista de
        // conhecidos, então é o único capaz de satisfazer a espera.
        await using AsyncServiceScope scopeA = api.Services.CreateAsyncScope();
        IMessageBus busA = scopeA.ServiceProvider.GetRequiredService<IMessageBus>();
        Task<Result> taskA = busA.InvokeAsync<Result>(new RemoverCalendarioDiasUteisCommand(idAlvo));

        await ConcorrenciaTestHelpers.AguardarBackendBloqueadoAsync(
            api, taskA, pidsConhecidos, TimeSpan.FromSeconds(5));

        await txAlvoHolder.CommitAsync();
        await txDecoyHolder.CommitAsync();

        Func<Task> act = async () => await taskA;
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "o xmin lido por busA ficou obsoleto assim que dbAlvoHolder commitou a própria escrita na mesma linha (ADR-0119 — RemoverCalendarioDiasUteisCommandHandler propaga sem catch)");

        await decoyBlockedTask;
        await txDecoyBlocked.RollbackAsync();
    }
}
