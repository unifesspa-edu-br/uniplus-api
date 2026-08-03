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
internal static class ConcorrenciaTestHelpers
{
    /// <summary>
    /// Faz poll em <c>pg_stat_activity</c> até observar um backend com
    /// <c>wait_event_type = 'Lock'</c> executando um <c>UPDATE</c> na tabela
    /// informada — prova direta de que outro backend está bloqueado esperando
    /// o lock que o chamador segura, em vez de inferir isso por um prazo
    /// fixo. Um lock de escrita conflitante entre duas linhas da MESMA linha
    /// aparece como espera por <c>transactionid</c> em <c>pg_locks</c> (não
    /// como lock de relação não concedido — <c>RowExclusiveLock</c> na
    /// relação é compatível consigo mesmo e fica concedido para os dois
    /// lados), por isso <c>pg_stat_activity.wait_event_type</c> é o sinal
    /// correto, não um join com <c>pg_class</c>. Usa uma conexão própria (não
    /// a de quem segura o lock, que está ocupada dentro da própria transação).
    /// </summary>
    public static async Task AguardarLockPendenteAsync(MonolitoApiFactory api, string tabela, Task tarefaQueDeveriaBloquear)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(tarefaQueDeveriaBloquear);

        await using AsyncServiceScope pollScope = api.Services.CreateAsyncScope();
        ConfiguracaoDbContext pollDb = pollScope.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        string padraoQuery = $"%{tabela}%";

        DateTimeOffset prazo = DateTimeOffset.UtcNow.AddSeconds(5);
        while (DateTimeOffset.UtcNow < prazo)
        {
            if (tarefaQueDeveriaBloquear.IsCompleted)
            {
                Assert.Fail("A tarefa completou antes de bloquear no lock — a corrida não foi forçada como esperado.");
            }

            int backendsBloqueados = await pollDb.Database
                .SqlQuery<int>(
                    $"""
                    SELECT count(*)::int AS "Value" FROM pg_stat_activity
                    WHERE wait_event_type = 'Lock' AND query ILIKE {padraoQuery}
                    """)
                .SingleAsync();

            if (backendsBloqueados > 0)
            {
                return;
            }

            await Task.Delay(20);
        }

        Assert.Fail("Nenhum backend ficou bloqueado esperando o lock dentro do prazo — a corrida não foi forçada.");
    }
}
