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

    private TransacaoRetidaDeTeste(AsyncServiceScope escopo, IDbContextTransaction transacao)
    {
        _escopo = escopo;
        _transacao = transacao;
    }

    public IServiceProvider Servicos => _escopo.ServiceProvider;

    public static async Task<TransacaoRetidaDeTeste> AbrirAsync(IServiceProvider servicos)
    {
        AsyncServiceScope escopo = servicos.CreateAsyncScope();
        IDbContextTransaction transacao = await escopo.ServiceProvider.GetRequiredService<AutorizacaoDbContext>()
            .Database.BeginTransactionAsync();

        return new TransacaoRetidaDeTeste(escopo, transacao);
    }

    /// <summary>Espera o banco registrar o número de sessões paradas numa trava: a operação concorrente chegou ao ponto da disputa.</summary>
    public static Task AguardarBloqueioAsync(IServiceProvider servicos, int sessoes = 1) =>
        PedidosDeConcessaoTestBase.AguardarAsync(async () =>
        {
            await using AsyncServiceScope escopo = servicos.CreateAsyncScope();

            return await escopo.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().Database
                .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM pg_stat_activity WHERE wait_event_type = 'Lock'")
                .SingleAsync() >= sessoes;
        },
        "as sessões esperadas não ficaram à espera de uma trava");

    public Task ConfirmarAsync() => _transacao.CommitAsync();

    public async ValueTask DisposeAsync()
    {
        await _transacao.DisposeAsync();
        await _escopo.DisposeAsync();
    }
}
