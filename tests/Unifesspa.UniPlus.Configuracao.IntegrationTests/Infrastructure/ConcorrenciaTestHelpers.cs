namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
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
    private static readonly TimeSpan PrazoPadrao = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Faz poll em <c>pg_stat_activity</c> até observar um backend com
    /// <c>wait_event_type = 'Lock'</c> que <c>pg_blocking_pids()</c> confirma
    /// estar bloqueado especificamente por um dos <paramref name="pidsSeguradoresDoLock"/>
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
    /// <param name="tarefaQueDeveriaBloquear">Tarefa do handler real sob teste — falha cedo se completar antes de bloquear.</param>
    /// <param name="pidsSeguradoresDoLock">
    /// PID de toda conexão que o próprio chamador mantém aberta segurando o
    /// lock (ex.: a transação com o <c>UPDATE</c> ainda não commitado) —
    /// capturado via <see cref="ObterPidDaConexaoAsync"/> ENQUANTO a conexão
    /// está ociosa (nunca enquanto ela mesma está executando um comando
    /// bloqueado — a mesma conexão Npgsql não aceita um novo comando
    /// concorrente ao que já está em voo).
    /// </param>
    /// <param name="prazo">Tempo máximo de espera; usa <see cref="PrazoPadrao"/> quando omitido.</param>
    public static async Task AguardarBackendBloqueadoAsync(
        MonolitoApiFactory api,
        Task tarefaQueDeveriaBloquear,
        IReadOnlyCollection<int> pidsSeguradoresDoLock,
        TimeSpan? prazo = null)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(tarefaQueDeveriaBloquear);
        ArgumentNullException.ThrowIfNull(pidsSeguradoresDoLock);

        await using AsyncServiceScope pollScope = api.Services.CreateAsyncScope();
        ConfiguracaoDbContext pollDb = pollScope.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        int pidDoPoll = await ObterPidDaConexaoAsync(pollDb);
        int[] pidsHolders = [.. pidsSeguradoresDoLock];

        DateTimeOffset limite = DateTimeOffset.UtcNow.Add(prazo ?? PrazoPadrao);
        while (DateTimeOffset.UtcNow < limite)
        {
            if (tarefaQueDeveriaBloquear.IsCompleted)
            {
                Assert.Fail("A tarefa completou antes de bloquear no lock — a corrida não foi forçada como esperado.");
            }

            int backendsBloqueados = await pollDb.Database
                .SqlQuery<int>(
                    $"""
                    SELECT count(*)::int AS "Value" FROM pg_stat_activity psa
                    WHERE psa.wait_event_type = 'Lock'
                      AND psa.pid <> {pidDoPoll}
                      AND EXISTS (
                          SELECT 1 FROM unnest(pg_blocking_pids(psa.pid)) AS bloqueador(pid)
                          WHERE bloqueador.pid = ANY({pidsHolders})
                      )
                    """)
                .SingleAsync();

            if (backendsBloqueados > 0)
            {
                return;
            }

            await Task.Delay(20);
        }

        Assert.Fail("Nenhum backend bloqueado especificamente pelos seguradores do lock apareceu dentro do prazo — a corrida não foi forçada.");
    }

    /// <summary>
    /// PID real da conexão Postgres por trás de <paramref name="conexao"/>.
    /// Só é seguro chamar enquanto a conexão está ociosa — nunca enquanto ela
    /// mesma está executando um comando ainda em voo (ex.: bloqueada esperando
    /// um lock): o protocolo do Npgsql não aceita um novo comando concorrente
    /// ao que já está em andamento na mesma conexão.
    /// </summary>
    public static async Task<int> ObterPidDaConexaoAsync(ConfiguracaoDbContext conexao)
    {
        ArgumentNullException.ThrowIfNull(conexao);
        return await conexao.Database
            .SqlQuery<int>($"""SELECT pg_backend_pid() AS "Value" """)
            .SingleAsync();
    }
}
