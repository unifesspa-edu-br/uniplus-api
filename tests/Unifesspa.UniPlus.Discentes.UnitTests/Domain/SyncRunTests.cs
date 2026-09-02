namespace Unifesspa.UniPlus.Discentes.UnitTests.Domain;

using AwesomeAssertions;

using Unifesspa.UniPlus.Discentes.Domain.Entities;
using Unifesspa.UniPlus.Discentes.Domain.Enums;
using Unifesspa.UniPlus.Discentes.UnitTests.Sigaa;

public sealed class SyncRunTests
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 2, 3, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Dia = new(2026, 9, 2);

    private readonly RelogioControlado _relogio = new(Agora);

    [Fact]
    public void Iniciar_marca_a_execucao_em_andamento_no_dia_de_referencia()
    {
        SyncRun execucao = SyncRun.Iniciar(Dia, _relogio);

        execucao.Id.Should().NotBe(Guid.Empty);
        execucao.Status.Should().Be(SyncRunStatus.Running);
        execucao.DataDeReferencia.Should().Be(Dia);
        execucao.StartedAt.Should().Be(Agora.UtcDateTime);
        execucao.FinishedAt.Should().BeNull();
    }

    [Fact]
    public void Iniciar_exige_relogio_injetado()
    {
        Action semRelogio = () => SyncRun.Iniciar(Dia, clock: null!);

        semRelogio.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Concluir_registra_as_contagens_e_o_instante_de_termino()
    {
        SyncRun execucao = SyncRun.Iniciar(Dia, _relogio);
        _relogio.Avancar(TimeSpan.FromMinutes(4));

        execucao.Concluir(new ContagensDaExecucao(1000, 990, 985, 5), SyncRunStatus.Completed, _relogio);

        execucao.Status.Should().Be(SyncRunStatus.Completed);
        execucao.TotalItems.Should().Be(1000);
        execucao.ProcessedItems.Should().Be(990);
        execucao.SuccessCount.Should().Be(985);
        execucao.ErrorCount.Should().Be(5);
        execucao.FinishedAt.Should().Be(Agora.AddMinutes(4).UtcDateTime);
    }

    [Theory]
    [InlineData(SyncRunStatus.Completed)]
    [InlineData(SyncRunStatus.Partial)]
    [InlineData(SyncRunStatus.Failed)]
    public void Concluir_aceita_os_tres_desfechos(SyncRunStatus desfecho)
    {
        SyncRun execucao = SyncRun.Iniciar(Dia, _relogio);

        execucao.Concluir(ContagensDaExecucao.Nenhuma, desfecho, _relogio);

        execucao.Status.Should().Be(desfecho);
    }

    [Theory]
    [InlineData(-1, 0, 0, 0)]
    [InlineData(0, -1, 0, 0)]
    [InlineData(0, 0, -1, 0)]
    [InlineData(0, 0, 0, -1)]
    public void Contagens_recusam_numero_negativo(int lidos, int processados, int aproveitados, int recusados)
    {
        Func<ContagensDaExecucao> negativa = () =>
            new ContagensDaExecucao(lidos, processados, aproveitados, recusados);

        negativa.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Contagens_recusam_tratar_mais_do_que_a_origem_entregou()
    {
        Func<ContagensDaExecucao> maisDoQueVeio = () => new ContagensDaExecucao(
            LidosNaOrigem: 1, Processados: 2, Aproveitados: 2, Recusados: 0);

        maisDoQueVeio.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Concluir_recusa_contagens_que_nao_fecham()
    {
        // Aproveitado e recusado são subconjuntos do que foi tratado. Somados além dele, o
        // registro da execução mostraria uma conta impossível a quem for lê-lo depois.
        Func<ContagensDaExecucao> somaMaiorQueOTratado = () => new ContagensDaExecucao(
            LidosNaOrigem: 10, Processados: 3, Aproveitados: 3, Recusados: 3);

        somaMaiorQueOTratado.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Concluir_recusa_deixar_a_execucao_em_andamento()
    {
        SyncRun execucao = SyncRun.Iniciar(Dia, _relogio);

        Action comEstadoNaoTerminal = () =>
            execucao.Concluir(ContagensDaExecucao.Nenhuma, SyncRunStatus.Running, _relogio);

        comEstadoNaoTerminal.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Concluir_uma_execucao_ja_encerrada_e_recusado()
    {
        SyncRun execucao = SyncRun.Iniciar(Dia, _relogio);
        execucao.Concluir(ContagensDaExecucao.Nenhuma, SyncRunStatus.Completed, _relogio);

        Action deNovo = () =>
            execucao.Concluir(ContagensDaExecucao.Nenhuma, SyncRunStatus.Failed, _relogio);

        deNovo.Should().Throw<InvalidOperationException>();
    }
}
