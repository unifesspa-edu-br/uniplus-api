namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;

/// <summary>
/// Transação do módulo Autorização mantida aberta pelo teste, com o escopo de serviços que a usa. Dá a ordem das
/// travas que duas operações concorrentes teriam, sem gancho no código de produção: o teste decide quando cada uma
/// confirma e espera o banco registrar a outra bloqueada.
/// </summary>
internal sealed class TransacaoRetidaDeTeste : IAsyncDisposable
{
    private readonly AsyncServiceScope _escopo;
    private readonly IDbContextTransaction _transacao;
    private readonly int _pid;

    private TransacaoRetidaDeTeste(AsyncServiceScope escopo, IDbContextTransaction transacao, int pid)
    {
        _escopo = escopo;
        _transacao = transacao;
        _pid = pid;
    }

    public IServiceProvider Servicos => _escopo.ServiceProvider;

    public static async Task<TransacaoRetidaDeTeste> AbrirAsync(IServiceProvider servicos)
    {
        AsyncServiceScope escopo = servicos.CreateAsyncScope();
        AutorizacaoDbContext contexto = escopo.ServiceProvider.GetRequiredService<AutorizacaoDbContext>();
        IDbContextTransaction transacao = await contexto.Database.BeginTransactionAsync();
        int pid = await contexto.Database.SqlQuery<int>($"SELECT pg_backend_pid() AS \"Value\"").SingleAsync();

        return new TransacaoRetidaDeTeste(escopo, transacao, pid);
    }

    /// <summary>
    /// Espera o banco registrar o número de sessões bloqueadas por esta transação, direta ou indiretamente (na fila atrás de
    /// outra que ela bloqueia), no banco do teste: a operação concorrente chegou ao ponto da disputa. Sessões presas em
    /// outras travas, de outros testes, não contam.
    /// </summary>
    public Task AguardarBloqueioAsync(int sessoes = 1) =>
        PedidosDeConcessaoTestBase.AguardarAsync(
            async () =>
            {
                await using AsyncServiceScope escopo = _escopo.ServiceProvider.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();

                return await escopo.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().Database
                    .SqlQuery<int>($"""
                        WITH RECURSIVE bloqueadas(pid) AS (
                            SELECT a.pid FROM pg_stat_activity a
                            WHERE a.datname = current_database() AND {_pid} = ANY(pg_blocking_pids(a.pid))
                            UNION
                            SELECT a.pid FROM pg_stat_activity a JOIN bloqueadas b ON b.pid = ANY(pg_blocking_pids(a.pid))
                            WHERE a.datname = current_database()
                        )
                        SELECT count(*)::int AS "Value" FROM bloqueadas
                        """)
                    .SingleAsync() >= sessoes;
            },
            "as sessões esperadas não ficaram bloqueadas pela transação retida do teste");

    public Task ConfirmarAsync() => _transacao.CommitAsync();

    public async ValueTask DisposeAsync()
    {
        await _transacao.DisposeAsync();
        await _escopo.DisposeAsync();
    }
}
