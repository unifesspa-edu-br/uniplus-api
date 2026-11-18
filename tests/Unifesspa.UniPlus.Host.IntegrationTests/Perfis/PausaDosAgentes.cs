namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using Wolverine.Transports;

/// <summary>
/// Agentes de escuta pausados por um teste e retomados quando a pausa é descartada. Cada agente entra na
/// lista <b>antes</b> de ser pausado: se a pausa de um deles falhar no meio, ou se a limpeza do teste falhar depois,
/// todos os que chegaram a ser tocados são retomados, e nenhum agente fica parado e contamina os testes seguintes.
/// </summary>
internal sealed class PausaDosAgentes : IAsyncDisposable
{
    private static readonly TimeSpan DuracaoDaPausa = TimeSpan.FromMinutes(10);

    private readonly List<IListeningAgent> _tocados = [];

    private PausaDosAgentes()
    {
    }

    public static async Task<PausaDosAgentes> PausarAsync(IEnumerable<IListeningAgent> agentes)
    {
        PausaDosAgentes pausa = new();

        try
        {
            await pausa.PausarTodosAsync(agentes);
        }
        catch (Exception falhaDaPausa)
        {
            await pausa.RetomarAposFalhaAsync(falhaDaPausa);
            throw;
        }

        return pausa;
    }

    private async Task RetomarAposFalhaAsync(Exception falhaDaPausa)
    {
        try
        {
            await DisposeAsync();
        }
        catch (OperationCanceledException)
        {
            // A falha da pausa é propagada pelo chamador.
        }
        catch (AggregateException falhasDaRetomada)
        {
            throw new AggregateException([falhaDaPausa, .. falhasDaRetomada.InnerExceptions]);
        }
    }

    private async Task PausarTodosAsync(IEnumerable<IListeningAgent> agentes)
    {
        foreach (IListeningAgent agente in agentes)
        {
            _tocados.Add(agente);
            await agente.PauseAsync(DuracaoDaPausa);
        }
    }

    /// <summary>
    /// Retoma cada agente isoladamente: a falha de um — mesmo uma falha síncrona ao iniciar a retomada — não impede a dos
    /// demais, e todas as falhas voltam juntas.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        IListeningAgent[] aRetomar = [.. _tocados];
        _tocados.Clear();

        Task todas = Task.WhenAll(aRetomar.Select(RetomarAsync));

        await todas.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

        if (todas.IsFaulted)
        {
            throw todas.Exception!;
        }

        await todas;
    }

    private static async Task RetomarAsync(IListeningAgent agente) => await agente.StartAsync();
}
