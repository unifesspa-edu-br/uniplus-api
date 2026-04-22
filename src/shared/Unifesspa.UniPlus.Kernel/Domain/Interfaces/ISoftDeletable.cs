namespace Unifesspa.UniPlus.Kernel.Domain.Interfaces;

public interface ISoftDeletable
{
    bool IsDeleted { get; }
    DateTimeOffset? DeletedAt { get; }
    string? DeletedBy { get; }
    void MarkAsDeleted(string deletedBy);
}
