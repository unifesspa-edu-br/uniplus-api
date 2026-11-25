namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Application.Commands.CalendariosDiasUteis;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
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
        (Guid targetId, Guid decoyId) = await CriarCalendariosAsync(api);
        await VerificarBloqueioPorTransacaoAlheiaAsync(api, targetId, decoyId);
    }

    private static async Task<(Guid TargetId, Guid DecoyId)> CriarCalendariosAsync(MonolitoApiFactory api)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        CalendarioDiasUteis target = CriarCalendario("target");
        CalendarioDiasUteis decoy = CriarCalendario("decoy");
        db.CalendariosDiasUteis.AddRange(target, decoy);
        await db.SaveChangesAsync();
        return (target.Id, decoy.Id);
    }

    private static CalendarioDiasUteis CriarCalendario(string finalidade) => CalendarioDiasUteis.Criar(
        $"pid-{finalidade}-{Guid.NewGuid():N}"[..20],
        [new DiaNaoUtilCriacao("NACIONAL", null, null, null, new DateOnly(2099, 1, 1), "x")]).Value!;

    private static async Task VerificarBloqueioPorTransacaoAlheiaAsync(MonolitoApiFactory api, Guid targetId, Guid decoyId)
    {
        await using AsyncServiceScope scopeHolder = api.Services.CreateAsyncScope();
        UniPlusDbContext dbHolder = scopeHolder.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        await using IDbContextTransaction txHolder = await dbHolder.Database.BeginTransactionAsync();
        await AtualizarCalendarioAsync(dbHolder, decoyId);
        int holderPid = await ConcorrenciaTestHelpers.GetConnectionPidAsync(dbHolder);
        await using AsyncServiceScope scopeBlocked = api.Services.CreateAsyncScope();
        UniPlusDbContext dbBlocked = scopeBlocked.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        await using IDbContextTransaction txBlocked = await dbBlocked.Database.BeginTransactionAsync();
        Task blockedTask = AtualizarCalendarioAsync(dbBlocked, decoyId);
        var corrida = new CorridaAlheia(txHolder, txBlocked, blockedTask, holderPid);
        await VerificarCorridasAsync(api, targetId, corrida);
    }

    private static Task<int> AtualizarCalendarioAsync(UniPlusDbContext db, Guid id) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE configuracao.calendarios_de_dias_uteis SET updated_at = now() WHERE id = {id}");

    private static async Task VerificarCorridasAsync(MonolitoApiFactory api, Guid targetId, CorridaAlheia corrida)
    {
        try
        {
            await ConcorrenciaTestHelpers.WaitForBlockedBackendAsync(
                api, corrida.TarefaBloqueada, [corrida.HolderPid], TimeSpan.FromSeconds(5));
            await VerificarCorridaDoCalendarioAsync(api, targetId, corrida);
        }
        finally
        {
            await LiberarCorridaAlheiaAsync(corrida);
        }
    }

    private static async Task VerificarCorridaDoCalendarioAsync(MonolitoApiFactory api, Guid targetId, CorridaAlheia corrida)
    {
        await using AsyncServiceScope scopeHolder = api.Services.CreateAsyncScope();
        UniPlusDbContext dbHolder = scopeHolder.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        await using IDbContextTransaction txHolder = await dbHolder.Database.BeginTransactionAsync();
        await AtualizarCalendarioAsync(dbHolder, targetId);
        int holderPid = await ConcorrenciaTestHelpers.GetConnectionPidAsync(dbHolder);
        await VerificarBloqueioAlheioIgnoradoAsync(api, holderPid);
        await VerificarConflitoDeConcorrenciaAsync(api, targetId, holderPid, txHolder, corrida);
    }

    private static async Task VerificarBloqueioAlheioIgnoradoAsync(MonolitoApiFactory api, int holderPid)
    {
        Task taskThatNeverActuallyBlocks = Task.Delay(TimeSpan.FromMinutes(1));
        Func<Task> waitWithoutBusA = () => ConcorrenciaTestHelpers.WaitForBlockedBackendAsync(
            api, taskThatNeverActuallyBlocks, [holderPid], TimeSpan.FromMilliseconds(300));
        await waitWithoutBusA.Should().ThrowAsync<FailException>(
            "o decoy está bloqueado por outro holder — pg_blocking_pids não o correlaciona com targetHolderPid");
    }

    private static async Task VerificarConflitoDeConcorrenciaAsync(
        MonolitoApiFactory api, Guid targetId, int holderPid, IDbContextTransaction txHolder, CorridaAlheia corrida)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        IMessageBus bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        Task<Result> task = bus.InvokeAsync<Result>(new RemoverCalendarioDiasUteisCommand(targetId));
        await ConcorrenciaTestHelpers.WaitForBlockedBackendAsync(api, task, [holderPid], TimeSpan.FromSeconds(5));
        await txHolder.CommitAsync();
        await corrida.TransacaoHolder.CommitAsync();
        corrida.HolderCommitado = true;
        Func<Task> act = async () => await task;
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "o xmin lido por busA ficou obsoleto assim que dbTargetHolder commitou a própria escrita na mesma linha (ADR-0119 — RemoverCalendarioDiasUteisCommandHandler propaga sem catch)");
        await FinalizarTransacaoBloqueadaAsync(corrida);
    }

    private static async Task LiberarCorridaAlheiaAsync(CorridaAlheia corrida)
    {
        if (!corrida.HolderCommitado)
        {
            await corrida.TransacaoHolder.RollbackAsync();
        }

        if (!corrida.BloqueadaFinalizada)
        {
            await FinalizarTransacaoBloqueadaAsync(corrida);
        }
    }

    private static async Task FinalizarTransacaoBloqueadaAsync(CorridaAlheia corrida)
    {
        await corrida.TarefaBloqueada;
        await corrida.TransacaoBloqueada.RollbackAsync();
        corrida.BloqueadaFinalizada = true;
    }

    private sealed record CorridaAlheia(
        IDbContextTransaction TransacaoHolder,
        IDbContextTransaction TransacaoBloqueada,
        Task TarefaBloqueada,
        int HolderPid)
    {
        public bool HolderCommitado { get; set; }
        public bool BloqueadaFinalizada { get; set; }
    }
}
