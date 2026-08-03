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
/// Postgres isolado — não porque o método garanta isso por construção. O
/// chamador ainda precisa enumerar toda conexão que ELE MESMO mantém aberta
/// deliberadamente (ex.: a transação que segura o lock); qualquer atividade de
/// fundo do framework (health check, etc.) que por coincidência ficasse
/// bloqueada permaneceria fora do alcance desta proteção — cenário considerado
/// extremamente improvável e fora de escopo, assim como já era para o texto de
/// query antes desta issue.
/// </remarks>
internal static class ConcorrenciaTestHelpers
{
    private static readonly TimeSpan PrazoPadrao = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Faz poll em <c>pg_stat_activity</c> até observar um backend com
    /// <c>wait_event_type = 'Lock'</c> cujo PID não está em
    /// <paramref name="pidsConhecidos"/> nem é o da própria conexão de poll —
    /// prova direta de que outro backend (o handler real sob teste) está
    /// bloqueado esperando o lock, sem depender de casar texto de query com o
    /// nome da tabela. Isso evita o falso positivo de uma query concorrente não
    /// relacionada que mencione a mesma tabela por coincidência (issue #1031)
    /// — o problema do casamento por texto não era a possibilidade de haver
    /// múltiplos backends bloqueados, e sim tratar QUALQUER um deles como prova
    /// da corrida, mesmo quando o texto batia por acaso com algo alheio.
    /// </summary>
    /// <param name="api">Factory da API sob teste, usada para abrir a conexão de poll.</param>
    /// <param name="tarefaQueDeveriaBloquear">Tarefa do handler real sob teste — falha cedo se completar antes de bloquear.</param>
    /// <param name="pidsConhecidos">
    /// PID de toda conexão que o próprio chamador mantém aberta deliberadamente
    /// (ex.: a transação que segura o lock de escrita) — capturado via
    /// <see cref="ObterPidDaConexaoAsync"/> ENQUANTO a conexão está ociosa
    /// (nunca enquanto ela mesma está executando um comando bloqueado — a
    /// mesma conexão Npgsql não aceita um novo comando concorrente ao que já
    /// está em voo). Excluído da checagem, para que só um backend genuinamente
    /// alheio a essas conexões conhecidas conte como prova de bloqueio.
    /// </param>
    /// <param name="prazo">Tempo máximo de espera; usa <see cref="PrazoPadrao"/> quando omitido.</param>
    public static async Task AguardarBackendBloqueadoAsync(
        MonolitoApiFactory api,
        Task tarefaQueDeveriaBloquear,
        IReadOnlyCollection<int> pidsConhecidos,
        TimeSpan? prazo = null)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(tarefaQueDeveriaBloquear);
        ArgumentNullException.ThrowIfNull(pidsConhecidos);

        await using AsyncServiceScope pollScope = api.Services.CreateAsyncScope();
        ConfiguracaoDbContext pollDb = pollScope.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        int pidDoPoll = await ObterPidDaConexaoAsync(pollDb);
        int[] pidsExcluidos = [.. pidsConhecidos, pidDoPoll];

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
                    SELECT count(*)::int AS "Value" FROM pg_stat_activity
                    WHERE wait_event_type = 'Lock'
                      AND pid <> ALL({pidsExcluidos})
                    """)
                .SingleAsync();

            if (backendsBloqueados > 0)
            {
                return;
            }

            await Task.Delay(20);
        }

        Assert.Fail("Nenhum backend alheio às conexões conhecidas ficou bloqueado esperando um lock dentro do prazo — a corrida não foi forçada.");
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
