namespace Unifesspa.UniPlus.Discentes.Infrastructure.Sincronizacao;

using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Discentes.Application.Abstractions;
using Unifesspa.UniPlus.Discentes.Domain.Entities;
using Unifesspa.UniPlus.Discentes.Domain.Enums;
using Unifesspa.UniPlus.Discentes.Domain.Interfaces;

/// <summary>
/// Anota o início e o desfecho de cada execução da sincronização.
/// </summary>
public interface IRegistroDeExecucoes
{
    Task<Guid> IniciarAsync(DateOnly dataDeReferencia, CancellationToken cancellationToken = default);

    Task ConcluirAsync(
        Guid execucaoId,
        ContagensDaExecucao contagens,
        SyncRunStatus situacao,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Anota cada marco em escopo próprio, com confirmação independente.
/// </summary>
/// <remarks>
/// O desfecho precisa sobreviver à falha que o provocou. Gravado no mesmo escopo de quem
/// executa a sincronização, ele seria desfeito junto quando a exceção subisse — e a
/// execução ficaria registrada como em andamento para sempre, justamente no caso em que
/// saber que ela fracassou importa.
/// </remarks>
internal sealed class RegistroDeExecucoes : IRegistroDeExecucoes
{
    private readonly IServiceScopeFactory _escopos;
    private readonly TimeProvider _relogio;

    public RegistroDeExecucoes(IServiceScopeFactory escopos, TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(escopos);
        ArgumentNullException.ThrowIfNull(relogio);

        _escopos = escopos;
        _relogio = relogio;
    }

    public async Task<Guid> IniciarAsync(
        DateOnly dataDeReferencia,
        CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope escopo = _escopos.CreateAsyncScope();

        SyncRun execucao = SyncRun.Iniciar(dataDeReferencia, _relogio);

        await Repositorio(escopo).AdicionarSyncRunAsync(execucao, cancellationToken).ConfigureAwait(false);
        await UnidadeDeTrabalho(escopo).SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return execucao.Id;
    }

    public async Task ConcluirAsync(
        Guid execucaoId,
        ContagensDaExecucao contagens,
        SyncRunStatus situacao,
        CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope escopo = _escopos.CreateAsyncScope();

        SyncRun execucao = await Repositorio(escopo).ObterSyncRunAsync(execucaoId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Execução {execucaoId} não encontrada para conclusão.");

        execucao.Concluir(contagens, situacao, _relogio);

        await UnidadeDeTrabalho(escopo).SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static ISyncRunRepository Repositorio(AsyncServiceScope escopo) =>
        escopo.ServiceProvider.GetRequiredService<ISyncRunRepository>();

    private static IDiscentesUnitOfWork UnidadeDeTrabalho(AsyncServiceScope escopo) =>
        escopo.ServiceProvider.GetRequiredService<IDiscentesUnitOfWork>();
}
