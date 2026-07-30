using AwesomeAssertions.Common;

using Unifesspa.UniPlus.Discentes.Domain.Entities;
using Unifesspa.UniPlus.Discentes.Domain.Enums;

namespace Unifesspa.UniPlus.Discentes.Domain;

public class SyncRunTests
{
    private readonly DateTimeOffset _agoraFixado = new(2026, 7, 30, 14, 0, 0, TimeSpan.Zero);
    private readonly TimeProvider _clock;

    public SyncRunTests()
    {
        _clock = new FakeTimeProvider(_agoraFixado);
    }
    [Fact(DisplayName = "SyncRun.Construtor Deve Lançar ArgumentException - Quando Guid For Vazio")]
    public void Construtor_DeveLancarArgumentException_QuandoGuidForVazio()
    {
        Assert.Throws<ArgumentException>(() =>
            new SyncRun(Guid.Empty, 10, _clock));
    }
    [Fact(DisplayName = "SyncRun.Construtor Deve Lançar ArgumentOutOfRangeException - Quando TotalItems For Negativo")]
    public void Construtor_DeveLancarArgumentOutOfRangeException_QuandoTotalItemsForNegativo()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SyncRun(Guid.NewGuid(), -1, _clock));
    }
    [Fact(DisplayName = "SyncRun.Construtor Deve Lancar ArgumentNullException - Quando Clock for Nulo")]
    public void Construtor_DeveLancarArgumentNullException_QuandoClockForNulo()
    {
        Assert.Throws<ArgumentNullException>(() => new SyncRun(Guid.NewGuid(), totalItems: 10, clock: null!));
    }

    [Fact(DisplayName = "SyncRun.Construtor Deve Inicializar com Sucesso - Atribuir Data Inicio do Clock")]
    public void Construtor_DeveInicializarComSucesso_EAtribuirDataInicioDoClock()
    {
        SyncRun syncRun = new SyncRun(Guid.NewGuid(), totalItems: 100, _clock);

        Assert.Equal(SyncRunStatus.Running, syncRun.Status);
        Assert.Equal(100, syncRun.TotalItems);
        Assert.Equal(_agoraFixado.UtcDateTime, syncRun.StartedAt);
        Assert.Null(syncRun.FinishedAt);
    }

    [Fact(DisplayName = "SyncRun.AtualizarProgresso Deve Atualizar Contadores Corretamente")]
    public void AtualizarProgresso_DeveAtualizarContadoresCorretamente()
    {
        SyncRun syncRun = new SyncRun(Guid.NewGuid(), totalItems: 10, _clock);

        syncRun.AtualizarProgresso(processedItems: 10, successCount: 8, errorCount: 2);

        Assert.Equal(10, syncRun.ProcessedItems);
        Assert.Equal(8, syncRun.SuccessCount);
        Assert.Equal(2, syncRun.ErrorCount);
    }
    [Fact(DisplayName = "SyncRun.AtualizarProgresso Deve Lançar Exceção - Quando Processados For Maior Que Total")]
    public void AtualizarProgresso_DeveLancarException_QuandoProcessadosMaiorQueTotal()
    {
        var syncRun = new SyncRun(Guid.NewGuid(), 100, _clock);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            syncRun.AtualizarProgresso(
                processedItems: 101,
                successCount: 90,
                errorCount: 11));
    }

    [Fact(DisplayName = "SyncRun.AtualizarProgresso Deve Lançar Exceção - Quando Soma Dos Contadores For Inválida")]
    public void AtualizarProgresso_DeveLancarException_QuandoSomaDosContadoresForInvalida()
    {
        var syncRun = new SyncRun(Guid.NewGuid(), 100, _clock);

        Assert.Throws<ArgumentException>(() =>
            syncRun.AtualizarProgresso(
                processedItems: 50,
                successCount: 40,
                errorCount: 20));
    }

    [Theory]
    [InlineData(SyncRunStatus.Completed)]
    [InlineData(SyncRunStatus.Partial)]
    [InlineData(SyncRunStatus.Failed)]
    public void Concluir_DeveTransicionarComSucesso_QuandoStatusForTerminal(SyncRunStatus statusTerminal)
    {
        SyncRun syncRun = new SyncRun(Guid.NewGuid(), totalItems: 100, _clock);

        syncRun.Concluir(statusTerminal, _clock);

        Assert.Equal(statusTerminal, syncRun.Status);
        Assert.Equal(_agoraFixado.UtcDateTime, syncRun.FinishedAt);
    }

    [Theory]
    [InlineData(SyncRunStatus.Running)]
    public void Concluir_DeveLancarArgumentException_QuandoStatusNaoForTerminal(SyncRunStatus statusInvalido)
    {
        SyncRun syncRun = new SyncRun(Guid.NewGuid(), totalItems: 100, _clock);

        ArgumentException exception = Assert.Throws<ArgumentException>(() => syncRun.Concluir(statusInvalido, _clock));
        Assert.Contains("não é um estado terminal válido", exception.Message);
    }

    [Fact(DisplayName = "SyncRun.Concluir Deve Lancar ArgumentNullException - Quando Clock for Nulo")]
    public void Concluir_DeveLancarArgumentNullException_QuandoClockForNulo()
    {
        SyncRun syncRun = new SyncRun(Guid.NewGuid(), totalItems: 100, _clock);

        Assert.Throws<ArgumentNullException>(() => syncRun.Concluir(SyncRunStatus.Completed, clock: null!));
    }

    [Fact(DisplayName = "SyncRun.Concluir Deve Lancar InvalidOperationException - Quando Tentar Reconcluir")]
    public void Concluir_DeveLancarInvalidOperationException_QuandoTentarReconcluir()
    {
        var syncRun = new SyncRun(Guid.NewGuid(), totalItems: 100, _clock);
        syncRun.Concluir(SyncRunStatus.Completed, _clock);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => syncRun.Concluir(SyncRunStatus.Failed, _clock));
        Assert.Contains("Transição de estado inválida", exception.Message);
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;
        public FakeTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
