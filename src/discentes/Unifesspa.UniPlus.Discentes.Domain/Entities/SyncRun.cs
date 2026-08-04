namespace Unifesspa.UniPlus.Discentes.Domain.Entities;

using Unifesspa.UniPlus.Discentes.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Entities;

public sealed class SyncRun : EntityBase
{
    public SyncRunStatus Status { get; private set; }
    public int TotalItems { get; private set; }
    public int ProcessedItems { get; private set; }
    public int SuccessCount { get; private set; }
    public int ErrorCount { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }

    private SyncRun() { }

    private SyncRun(int totalItems, DateTime startedAt)
    {
        Status = SyncRunStatus.Running;
        TotalItems = totalItems;
        StartedAt = startedAt;
    }

    /// <param name="clock">
    /// Fonte de "agora" para <c>StartedAt</c>. Obrigatório (sem default
    /// <see cref="TimeProvider.System"/>): a convenção de relógio exige que o
    /// <see cref="TimeProvider"/> seja sempre injetado. Testes passam um
    /// <see cref="TimeProvider"/> fake para isolar o cenário do relógio.
    /// </param>
    public static SyncRun Criar(int totalItems, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentOutOfRangeException.ThrowIfNegative(totalItems);

        return new SyncRun(totalItems, clock.GetUtcNow().UtcDateTime);
    }

    public void AtualizarProgresso(int processedItems, int successCount, int errorCount)
    {
        if (Status != SyncRunStatus.Running)
            throw new InvalidOperationException(
                $"Não é possível atualizar o progresso de um SyncRun no estado '{Status}'.");

        ArgumentOutOfRangeException.ThrowIfNegative(processedItems);
        ArgumentOutOfRangeException.ThrowIfNegative(successCount);
        ArgumentOutOfRangeException.ThrowIfNegative(errorCount);

        if (processedItems > TotalItems)
            throw new ArgumentOutOfRangeException(
                nameof(processedItems),
                "A quantidade processada não pode ser maior que o total de itens.");

        if (successCount + errorCount > processedItems)
            throw new ArgumentException(
                "A soma de sucessos e erros não pode ser maior que a quantidade processada.");

        ProcessedItems = processedItems;
        SuccessCount = successCount;
        ErrorCount = errorCount;
    }

    public void Concluir(SyncRunStatus finalStatus, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        if (Status != SyncRunStatus.Running)
        {
            throw new InvalidOperationException(
                $"Transição de estado inválida. Não é possível concluir um SyncRun no estado '{Status}'. " +
                "A conclusão só é permitida para execuções no estado 'Running'.");
        }

        if (finalStatus is not (SyncRunStatus.Completed or SyncRunStatus.Partial or SyncRunStatus.Failed))
        {
            throw new ArgumentException(
                $"Estado '{finalStatus}' não é um estado terminal válido. " +
                "A conclusão exige um dos estados: Completed, Partial ou Failed.", nameof(finalStatus));
        }

        Status = finalStatus;
        FinishedAt = clock.GetUtcNow().UtcDateTime;
    }
}
