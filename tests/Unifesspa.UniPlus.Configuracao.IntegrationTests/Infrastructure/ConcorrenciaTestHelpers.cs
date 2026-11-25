namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

using Xunit;

/// <summary>
/// Suporte compartilhado para testes de corrida real de concorrência otimista
/// (ADR-0119) que precisam forçar, de forma determinística, que um handler
/// real fique bloqueado tentando escrever numa linha travada por outra
/// transação — sem depender de <c>Task.WhenAll</c>/prazos fixos, que não
/// reproduzem a janela real de forma confiável sob carga (ver
/// <c>CalendarioDiasUteisConcorrenciaTests</c>/<c>TermoConsentimentoConcorrenciaTests</c>).
/// </summary>
/// <remarks>
/// A identificação por PID real da conexão bloqueada (em vez de por texto de
/// query, issue #1031) só é confiável porque a collection que chama este
/// helper roda com <c>DisableParallelization = true</c> (nenhum outro teste da
/// mesma suíte roda ao mesmo tempo) e cada módulo usa seu próprio container
/// Postgres isolado — não porque o método garanta isso por construção.
/// </remarks>
internal static class ConcorrenciaTestHelpers
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Faz poll em <c>pg_stat_activity</c> até observar um backend com
    /// <c>wait_event_type = 'Lock'</c> que <c>pg_blocking_pids()</c> confirma
    /// estar bloqueado especificamente por um dos <paramref name="lockHolderPids"/>
    /// — prova direta de que outro backend (o handler real sob teste) está
    /// esperando o lock que o chamador segura, sem depender de casar texto de
    /// query com o nome da tabela nem de simplesmente excluir PIDs conhecidos.
    /// </summary>
    /// <remarks>
    /// Excluir PIDs conhecidos sozinho (versão anterior deste helper) ainda
    /// arriscava falso positivo: qualquer backend alheio à corrida — atividade
    /// de fundo do runtime Wolverine, por exemplo — que estivesse esperando
    /// QUALQUER outro lock, não relacionado, também seria contado como prova.
    /// Correlacionar via <c>pg_blocking_pids</c> (quem está bloqueando o
    /// candidato) elimina esse risco: só conta um backend cujo bloqueador
    /// comprovado é uma das conexões que o chamador sabe que está segurando o
    /// lock — não bloqueado por QUALQUER OUTRA coisa.
    /// </remarks>
    /// <param name="api">Factory da API sob teste, usada para abrir a conexão de poll.</param>
    /// <param name="taskExpectedToBlock">Tarefa do handler real sob teste — falha cedo se completar antes de bloquear.</param>
    /// <param name="lockHolderPids">
    /// PID de toda conexão que o próprio chamador mantém aberta segurando o
    /// lock (ex.: a transação com o <c>UPDATE</c> ainda não commitado) —
    /// capturado via <see cref="GetConnectionPidAsync"/> ENQUANTO a conexão
    /// está ociosa (nunca enquanto ela mesma está executando um comando
    /// bloqueado — a mesma conexão Npgsql não aceita um novo comando
    /// concorrente ao que já está em voo).
    /// </param>
    /// <param name="timeout">Tempo máximo de espera; usa <see cref="DefaultTimeout"/> quando omitido.</param>
    public static async Task WaitForBlockedBackendAsync(
        MonolitoApiFactory api,
        Task taskExpectedToBlock,
        IReadOnlyCollection<int> lockHolderPids,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(taskExpectedToBlock);
        ArgumentNullException.ThrowIfNull(lockHolderPids);

        await using AsyncServiceScope pollScope = api.Services.CreateAsyncScope();
        UniPlusDbContext pollDb = pollScope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        await AguardarBloqueioAsync(pollDb, taskExpectedToBlock, [.. lockHolderPids], timeout ?? DefaultTimeout);
    }

    private static async Task AguardarBloqueioAsync(
        UniPlusDbContext pollDb, Task taskExpectedToBlock, int[] holderPids, TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            GarantirTarefaEmAndamento(taskExpectedToBlock);

            if (await ExisteBackendBloqueadoAsync(pollDb, holderPids))
            {
                return;
            }

            await Task.Delay(20);
        }

        Assert.Fail("Nenhum backend bloqueado especificamente pelos seguradores do lock apareceu dentro do prazo — a corrida não foi forçada.");
    }

    private static void GarantirTarefaEmAndamento(Task taskExpectedToBlock)
    {
        if (taskExpectedToBlock.IsCompleted)
        {
            Assert.Fail("A tarefa completou antes de bloquear no lock — a corrida não foi forçada como esperado.");
        }
    }

    private static async Task<bool> ExisteBackendBloqueadoAsync(UniPlusDbContext pollDb, int[] holderPids)
    {
        return await pollDb.Database
            .SqlQuery<int>(
                $"""
                SELECT count(*)::int AS "Value" FROM pg_stat_activity psa
                WHERE psa.wait_event_type = 'Lock'
                  AND EXISTS (
                      SELECT 1 FROM unnest(pg_blocking_pids(psa.pid)) AS blocker(pid)
                      WHERE blocker.pid = ANY({holderPids})
                  )
                """)
            .SingleAsync() > 0;
    }

    /// <summary>
    /// PID real da conexão Postgres por trás de <paramref name="connection"/>.
    /// Só é seguro chamar enquanto a conexão está ociosa — nunca enquanto ela
    /// mesma está executando um comando ainda em voo (ex.: bloqueada esperando
    /// um lock): o protocolo do Npgsql não aceita um novo comando concorrente
    /// ao que já está em andamento na mesma conexão.
    /// </summary>
    public static async Task<int> GetConnectionPidAsync(UniPlusDbContext connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return await connection.Database
            .SqlQuery<int>($"""SELECT pg_backend_pid() AS "Value" """)
            .SingleAsync();
    }
}
