namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Globalization;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.Kernel.Results;

using Wolverine;

/// <summary>
/// As operações reais que o teste põe, uma a uma e em ordem conhecida, na fila do bloqueio que a sessão retentora segura. Mantém
/// todas as tarefas iniciadas, mesmo que a montagem da fila falhe ou a espera estoure: ao ser descartada cancela as que ainda
/// esperam, solta a linha e aguarda o fim de todas, para que nenhuma continue rodando durante a limpeza do teste. Também é dona
/// do host em que as operações rodam. Se alguma não termina dentro do limite, o descarte falha, o host não é descartado e quem
/// instanciou a fila é avisado de que a fixture ficou inutilizável, para que a limpeza dos dados não concorra com o trabalho vivo.
/// </summary>
internal sealed class OperacoesNaFilaDoBloqueio : IAsyncDisposable
{
    private readonly CancellationTokenSource _cancelamento = new();
    private readonly List<(string Comando, Task<Result> Tarefa)> _operacoes = [];
    private readonly Func<object, CancellationToken, Task<Result>> _executar;
    private readonly IRetencaoDoProcesso _retida;
    private readonly IAsyncDisposable _hospedeiro;
    private readonly TimeSpan _espera;
    private readonly Action _aoFicarInutilizavel;

    public OperacoesNaFilaDoBloqueio(WebApplicationFactory<HostAssemblyMarker> host, IRetencaoDoProcesso retida, TimeSpan espera, Action aoFicarInutilizavel)
        : this((comando, cancelamento) => ExecutarNoBarramentoAsync(host.Services, comando, cancelamento), retida, host, espera, aoFicarInutilizavel)
    {
    }

    public OperacoesNaFilaDoBloqueio(
        Func<object, CancellationToken, Task<Result>> executar,
        IRetencaoDoProcesso retida,
        IAsyncDisposable hospedeiro,
        TimeSpan espera,
        Action aoFicarInutilizavel)
    {
        _executar = executar;
        _retida = retida;
        _hospedeiro = hospedeiro;
        _espera = espera;
        _aoFicarInutilizavel = aoFicarInutilizavel;
    }

    /// <summary>Envia cada comando e só passa ao seguinte quando o banco o mostra na fila do bloqueio.</summary>
    public async Task EnfileirarAsync(IEnumerable<object> comandos)
    {
        ArgumentNullException.ThrowIfNull(comandos);

        foreach (object comando in comandos)
        {
            _operacoes.Add((comando.GetType().Name, _executar(comando, _cancelamento.Token)));
            await _retida.AguardarOperacoesEsperandoAsync(_operacoes.Count);
        }
    }

    /// <summary>Solta a linha e devolve o resultado de cada operação, na ordem em que foram enfileiradas.</summary>
    public async Task<IReadOnlyList<Result>> LiberarEAguardarAsync()
    {
        await _retida.LiberarAsync();

        return await Task.WhenAll(_operacoes.Select(operacao => operacao.Tarefa)).WaitAsync(_espera);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _cancelamento.CancelAsync();
            await _retida.LiberarAsync();
            await AguardarOTerminoDeTodasAsync();
        }
        finally
        {
            _cancelamento.Dispose();
            await DescartarOHospedeiroSeNenhumaOperacaoEstiverAtivaAsync();
        }
    }

    private async Task AguardarOTerminoDeTodasAsync()
    {
        Task todas = Task.WhenAll(_operacoes.Select(operacao => operacao.Tarefa));
        using CancellationTokenSource limite = new();

        if (await Task.WhenAny(todas, Task.Delay(_espera, limite.Token)) != todas)
        {
            throw new TimeoutException(DescreverOperacoesAindaAtivas());
        }

        await limite.CancelAsync();

        if (todas.Exception is { } falhas)
        {
            throw falhas;
        }
    }

    private async Task DescartarOHospedeiroSeNenhumaOperacaoEstiverAtivaAsync()
    {
        if (_operacoes.Exists(operacao => !operacao.Tarefa.IsCompleted))
        {
            _aoFicarInutilizavel();
            return;
        }

        await _hospedeiro.DisposeAsync();
    }

    private string DescreverOperacoesAindaAtivas()
    {
        List<IGrouping<string, (string Comando, Task<Result> Tarefa)>> ativas = [.. _operacoes
            .Where(operacao => !operacao.Tarefa.IsCompleted)
            .GroupBy(operacao => operacao.Comando)];
        string porComando = string.Join(", ", ativas.Select(comando => $"{comando.Key} ({comando.Count().ToString(CultureInfo.InvariantCulture)})"));

        return $"{ativas.Sum(comando => comando.Count()).ToString(CultureInfo.InvariantCulture)} de {_operacoes.Count.ToString(CultureInfo.InvariantCulture)} " +
            $"operações seguem ativas {_espera.ToString("c", CultureInfo.InvariantCulture)} depois do cancelamento e da liberação da linha: {porComando}. " +
            "O host e a limpeza dos dados foram pulados para não concorrerem com o trabalho vivo; a fixture da coleção ficou inutilizável " +
            "e a execução seguinte dela não é confiável.";
    }

    private static async Task<Result> ExecutarNoBarramentoAsync(IServiceProvider services, object comando, CancellationToken cancelamento)
    {
        await using AsyncServiceScope escopo = services.CreateAsyncScope();
        IMessageBus bus = escopo.ServiceProvider.GetRequiredService<IMessageBus>();

        return await bus.InvokeAsync<Result>(comando, cancelamento);
    }
}
