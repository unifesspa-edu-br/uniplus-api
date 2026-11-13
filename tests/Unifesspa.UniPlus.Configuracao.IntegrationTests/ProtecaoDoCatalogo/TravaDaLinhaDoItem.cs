namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ProtecaoDoCatalogo;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

using Xunit;

/// <summary>
/// Uma transação real que trava a linha do item (<c>SELECT ... FOR UPDATE</c>, que não muda o <c>xmin</c>) para pôr em
/// fila, na ordem em que o teste as dispara, as gravações concorrentes das chamadas ao host: cada uma lê o item, bloqueia
/// ao gravar e só segue quando a trava é liberada. A primeira grava; a segunda, reavaliada sobre a versão nova da linha,
/// não casa o <c>xmin</c> que leu.
/// </summary>
/// <remarks>
/// A coleção roda sem paralelismo e o módulo tem o próprio banco: os backends contados são os da corrida. A contagem
/// inclui quem espera atrás de outro que espera, porque a segunda gravação aguarda a primeira na fila da linha.
/// </remarks>
internal sealed class TravaDaLinhaDoItem : IAsyncDisposable
{
    private static readonly TimeSpan Prazo = TimeSpan.FromSeconds(10);

    private readonly MonolitoApiFactory _api;
    private readonly AsyncServiceScope _escopo;
    private readonly IDbContextTransaction _transacao;
    private readonly int _pid;

    private TravaDaLinhaDoItem(MonolitoApiFactory api, AsyncServiceScope escopo, IDbContextTransaction transacao, int pid)
    {
        _api = api;
        _escopo = escopo;
        _transacao = transacao;
        _pid = pid;
    }

    /// <summary>A transação da trava, para o teste gravar junto dela o que quer ver confirmado na liberação.</summary>
    public ConfiguracaoDbContext Contexto => _escopo.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();

    public static async Task<TravaDaLinhaDoItem> TravarAsync(MonolitoApiFactory api, string cadastro, Guid id)
    {
        AsyncServiceScope escopo = api.Services.CreateAsyncScope();
        ConfiguracaoDbContext contexto = escopo.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        IDbContextTransaction transacao = await contexto.Database.BeginTransactionAsync();
        FormattableString trava = cadastro == "FATO"
            ? (FormattableString)$"SELECT 1 FROM configuracao.rol_de_fatos_candidato WHERE id = {id} FOR UPDATE"
            : (FormattableString)$"SELECT 1 FROM configuracao.tipo_documento WHERE id = {id} FOR UPDATE";
        await contexto.Database.ExecuteSqlInterpolatedAsync(trava);
        int pid = await contexto.Database.SqlQuery<int>($"""SELECT pg_backend_pid() AS "Value" """).SingleAsync();
        return new TravaDaLinhaDoItem(api, escopo, transacao, pid);
    }

    /// <summary>Espera <paramref name="quantidade"/> gravações na fila da linha; falha se a tarefa termina antes de bloquear.</summary>
    public async Task EsperarNaFilaAsync(Task tarefa, int quantidade)
    {
        DateTimeOffset limite = DateTimeOffset.UtcNow.Add(Prazo);
        while (DateTimeOffset.UtcNow < limite)
        {
            Assert.False(tarefa.IsCompleted, "A chamada terminou antes de entrar na fila da linha do item: a corrida não foi forçada.");
            if (await NaFilaAsync() >= quantidade)
            {
                return;
            }

            await Task.Delay(20);
        }

        Assert.Fail($"Não houve {quantidade} gravação(ões) na fila da linha do item dentro do prazo: a corrida não foi forçada.");
    }

    public Task LiberarAsync() => _transacao.CommitAsync();

    public async ValueTask DisposeAsync()
    {
        await _transacao.DisposeAsync();
        await _escopo.DisposeAsync();
    }

    // Quem a trava bloqueia e, transitivamente, quem espera atrás deles. A consulta sai de uma conexão fora da
    // transação da trava: dentro dela, a visão de pg_stat_activity ficaria congelada.
    private async Task<int> NaFilaAsync()
    {
        await using AsyncServiceScope escopo = _api.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>().Database.SqlQuery<int>(
            $"""
            WITH RECURSIVE fila(pid) AS (
                SELECT psa.pid FROM pg_stat_activity psa WHERE psa.wait_event_type = 'Lock' AND {_pid} = ANY(pg_blocking_pids(psa.pid))
                UNION
                SELECT psa.pid FROM pg_stat_activity psa JOIN fila ON fila.pid = ANY(pg_blocking_pids(psa.pid)) WHERE psa.wait_event_type = 'Lock'
            )
            SELECT count(*)::int AS "Value" FROM fila
            """).SingleAsync();
    }
}
