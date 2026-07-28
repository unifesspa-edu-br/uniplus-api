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

    public SyncRun(Guid id, int totalItems)
    {
        Id = id;
        Status = SyncRunStatus.Running;
        TotalItems = totalItems;
        StartedAt = DateTime.UtcNow;
    }

    public void UpdateProgress(int processed, int success, int errors)
    {
        ProcessedItems = processed;
        SuccessCount = success;
        ErrorCount = errors;
    }

    public void Complete(SyncRunStatus finalStatus)
    {
        Status = finalStatus;
        FinishedAt = DateTime.UtcNow;
    }
}
