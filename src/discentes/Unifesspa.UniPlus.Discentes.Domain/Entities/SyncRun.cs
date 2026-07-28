using Unifesspa.UniPlus.Discentes.Domain.Enums;

namespace Unifesspa.UniPlus.Discentes.Domain.Entities;

public class SyncRun
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public SyncRunStatus Status { get; private set; }
    public int TotalItems { get; private set; }
    public int ProcessedItems { get; private set; }
    public int SuccessCount { get; private set; }
    public int ErrorCount { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }

    private SyncRun() { }

    /// <param name="clock">
    /// Fonte de "agora" para <c>StartedAt</c>. Obrigatório (sem default
    /// <see cref="TimeProvider.System"/>): a convenção de relógio exige que o
    /// <see cref="TimeProvider"/> seja sempre injetado. Testes passam um
    /// <see cref="TimeProvider"/> fake para isolar o cenário do relógio.
    /// </param>
    public SyncRun(Guid id, int totalItems, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        Id = id;
        Status = SyncRunStatus.Running;
        TotalItems = totalItems;
        StartedAt = clock.GetUtcNow().UtcDateTime;
    }

    public void AtualizarProgresso(int processed, int success, int errors)
    {
        ProcessedItems = processed;
        SuccessCount = success;
        ErrorCount = errors;
    }

    public void Completo(SyncRunStatus finalStatus, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        Status = finalStatus;
        FinishedAt = clock.GetUtcNow().UtcDateTime;
    }
}
