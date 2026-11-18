namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDoProcesso;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

/// <summary>
/// O descarte da fila de operações sem banco nem host: com uma operação que ignora o cancelamento e passa do limite, a falha cita o
/// comando pendente, a fixture é marcada como inutilizável e o host não é descartado sob operação ativa; com todas terminadas,
/// o host é descartado e nada é marcado.
/// </summary>
public sealed class OperacoesNaFilaDoBloqueioTests
{
    private static readonly TimeSpan LimiteCurto = TimeSpan.FromMilliseconds(100);

    [Fact(DisplayName = "Operação que ignora o cancelamento e passa do limite falha o descarte citando o comando pendente, marca a fixture como inutilizável e mantém o host")]
    public async Task Descarte_ComOperacaoQueNaoTermina_FalhaMarcaInutilizavelEMantemOHost()
    {
        TaskCompletionSource<Result> semTermino = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ControleDoDescarte controle = new();
        OperacoesNaFilaDoBloqueio fila = new((_, _) => semTermino.Task, controle, controle, LimiteCurto, controle.MarcarInutilizavel);
        await fila.EnfileirarAsync([ComandoDeInclusao()]);

        try
        {
            await ConferirFalhaDoDescarteAsync(fila);
            ConferirQueAFixtureFicouInutilizavelComOHostMantido(controle);
        }
        finally
        {
            semTermino.SetResult(Result.Success());
        }
    }

    [Fact(DisplayName = "Operações que terminam ao serem canceladas descartam o host e não marcam a fixture como inutilizável")]
    public async Task Descarte_ComOperacoesQueTerminam_DescartaOHostSemMarcarInutilizavel()
    {
        ControleDoDescarte controle = new();
        OperacoesNaFilaDoBloqueio fila = new(EsperarOCancelamentoAsync, controle, controle, LimiteCurto, controle.MarcarInutilizavel);
        await fila.EnfileirarAsync([ComandoDeInclusao(), ComandoDeInclusao()]);

        await fila.DisposeAsync();

        controle.Liberada.Should().BeTrue();
        controle.HostDescartado.Should().BeTrue();
        controle.Inutilizavel.Should().BeFalse();
    }

    private static async Task ConferirFalhaDoDescarteAsync(OperacoesNaFilaDoBloqueio fila)
    {
        Func<Task> descartar = async () => await fila.DisposeAsync();

        (await descartar.Should().ThrowAsync<TimeoutException>()).Which.Message.Should()
            .Contain("1 de 1 operações seguem ativas")
            .And.Contain($"{nameof(IncluirMotivoNoProcessoCommand)} (1)")
            .And.Contain("execução seguinte dela não é confiável");
    }

    private static void ConferirQueAFixtureFicouInutilizavelComOHostMantido(ControleDoDescarte controle)
    {
        controle.Liberada.Should().BeTrue("a linha é solta antes de esperar pelas operações");
        controle.Inutilizavel.Should().BeTrue();
        controle.HostDescartado.Should().BeFalse("o host não é descartado sob operação ativa");
    }

    private static IncluirMotivoNoProcessoCommand ComandoDeInclusao() =>
        new(Guid.CreateVersion7(), LugarDaDecisao.DaFase(Guid.CreateVersion7()), Guid.CreateVersion7());

    private static async Task<Result> EsperarOCancelamentoAsync(object comando, CancellationToken cancelamento)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancelamento);

        return Result.Success();
    }

    private sealed class ControleDoDescarte : IRetencaoDoProcesso, IAsyncDisposable
    {
        public bool Liberada { get; private set; }

        public bool HostDescartado { get; private set; }

        public bool Inutilizavel { get; private set; }

        public Task AguardarOperacoesEsperandoAsync(int quantidade) => Task.CompletedTask;

        public Task LiberarAsync()
        {
            Liberada = true;

            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            HostDescartado = true;

            return ValueTask.CompletedTask;
        }

        public void MarcarInutilizavel() => Inutilizavel = true;
    }
}
