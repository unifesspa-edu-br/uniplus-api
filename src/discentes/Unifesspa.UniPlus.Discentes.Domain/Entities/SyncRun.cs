namespace Unifesspa.UniPlus.Discentes.Domain.Entities;

using Unifesspa.UniPlus.Discentes.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Entities;

/// <summary>
/// Registro de uma execução da sincronização com o SIGAA.
/// </summary>
public sealed class SyncRun : EntityBase
{
    private SyncRun()
    {
    }

    private SyncRun(DateOnly dataDeReferencia, DateTime iniciadaEm)
    {
        Status = SyncRunStatus.Running;
        DataDeReferencia = dataDeReferencia;
        StartedAt = iniciadaEm;
    }

    public SyncRunStatus Status { get; private set; }

    /// <summary>
    /// Dia a que a execução se refere — não o instante em que rodou. Uma execução
    /// disparada às 3h da manhã de um dia se refere àquele dia, e é assim que se reconhece
    /// que ele já foi sincronizado.
    /// </summary>
    public DateOnly DataDeReferencia { get; private set; }

    /// <summary>Vínculos que a origem entregou.</summary>
    public int TotalItems { get; private set; }

    /// <summary>Vínculos que a execução chegou a tratar.</summary>
    public int ProcessedItems { get; private set; }

    /// <summary>Vínculos que entraram na réplica ou que já estavam iguais nela.</summary>
    public int SuccessCount { get; private set; }

    /// <summary>Vínculos que a réplica não recebeu: recusados ou não gravados por falha.</summary>
    public int ErrorCount { get; private set; }

    public DateTime StartedAt { get; private set; }

    public DateTime? FinishedAt { get; private set; }

    /// <param name="clock">
    /// Fonte de "agora", sempre injetada: testes fornecem um relógio controlado para
    /// isolar o cenário do tempo real.
    /// </param>
    public static SyncRun Iniciar(DateOnly dataDeReferencia, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        return new SyncRun(dataDeReferencia, clock.GetUtcNow().UtcDateTime);
    }

    /// <summary>
    /// Encerra a execução com o que ela alcançou.
    /// </summary>
    public void Concluir(ContagensDaExecucao contagens, SyncRunStatus situacaoFinal, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(contagens);
        ArgumentNullException.ThrowIfNull(clock);

        if (Status != SyncRunStatus.Running)
        {
            throw new InvalidOperationException(
                $"Só uma execução em andamento pode ser concluída; esta está em '{Status}'.");
        }

        if (situacaoFinal is not (SyncRunStatus.Completed or SyncRunStatus.Partial or SyncRunStatus.Failed))
        {
            throw new ArgumentException(
                $"'{situacaoFinal}' não encerra uma execução.", nameof(situacaoFinal));
        }

        TotalItems = contagens.LidosNaOrigem;
        ProcessedItems = contagens.Processados;
        SuccessCount = contagens.Aproveitados;
        ErrorCount = contagens.Recusados;
        Status = situacaoFinal;
        FinishedAt = clock.GetUtcNow().UtcDateTime;
    }
}

/// <summary>
/// O que uma execução alcançou, em números.
/// </summary>
/// <remarks>
/// As relações entre eles são verificadas na construção: nenhum é negativo, não se trata
/// mais do que a origem entregou, e o que foi aproveitado somado ao que foi recusado cabe
/// no que foi tratado. Sem isso, um registro de execução impossível entraria no banco e
/// enganaria quem o lesse depois.
/// </remarks>
public sealed record ContagensDaExecucao
{
    public ContagensDaExecucao(int LidosNaOrigem, int Processados, int Aproveitados, int Recusados)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(LidosNaOrigem);
        ArgumentOutOfRangeException.ThrowIfNegative(Processados);
        ArgumentOutOfRangeException.ThrowIfNegative(Aproveitados);
        ArgumentOutOfRangeException.ThrowIfNegative(Recusados);

        if (Processados > LidosNaOrigem)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Processados),
                "Não se trata mais vínculos do que a origem entregou.");
        }

        if (Aproveitados + Recusados > Processados)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Aproveitados),
                "Aproveitados e recusados são subconjuntos do que foi tratado; somados não "
                + "podem exceder esse total.");
        }

        this.LidosNaOrigem = LidosNaOrigem;
        this.Processados = Processados;
        this.Aproveitados = Aproveitados;
        this.Recusados = Recusados;
    }

    /// <summary>Vínculos que a origem entregou.</summary>
    public int LidosNaOrigem { get; }

    /// <summary>Vínculos que a execução chegou a tratar.</summary>
    public int Processados { get; }

    /// <summary>Vínculos que entraram na réplica ou já estavam iguais nela.</summary>
    public int Aproveitados { get; }

    /// <summary>Vínculos que a réplica não recebeu.</summary>
    public int Recusados { get; }

    public static ContagensDaExecucao Nenhuma { get; } = new(0, 0, 0, 0);
}
