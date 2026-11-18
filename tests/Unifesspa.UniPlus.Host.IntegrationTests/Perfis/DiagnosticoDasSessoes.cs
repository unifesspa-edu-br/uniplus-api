namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>Consulta o estado da sessão que retém a transação e das sessões do banco em espera.</summary>
internal sealed class DiagnosticoDasSessoes(IServiceProvider servicos, int pid, string inicioDaSessao)
{
    public async Task<string> LerAsync()
    {
        await using AsyncServiceScope escopo = servicos.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>().Database.SqlQuery<string>($"""
            SELECT 'Transação retida (pid ' || {pid} || '): '
                || coalesce((SELECT 'estado=' || state || ', xact_start=' || coalesce(xact_start::text, 'nenhuma')
                    FROM pg_stat_activity WHERE pid = {pid}), 'sessão encerrada')
                || '. Início da sessão: guardado=' || {inicioDaSessao} || ', atual='
                || coalesce((SELECT backend_start::text FROM pg_stat_activity WHERE pid = {pid}), 'nenhuma')
                || ', postmaster=' || pg_postmaster_start_time()::text || '. Sessões do banco em espera: '
                || coalesce((SELECT string_agg(pid || ' ' || wait_event_type || '/' || wait_event
                    || ' bloqueada_por=' || pg_blocking_pids(pid)::text, '; ')
                    FROM pg_stat_activity WHERE datname = current_database()
                    AND wait_event_type IS NOT NULL AND state <> 'idle'), 'nenhuma') AS "Value"
            """).SingleAsync();
    }
}
