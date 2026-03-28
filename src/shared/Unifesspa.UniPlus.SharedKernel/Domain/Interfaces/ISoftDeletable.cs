namespace Unifesspa.UniPlus.SharedKernel.Domain.Interfaces;

public interface ISoftDeletable
{
    bool IsDeleted { get; }
    DateTimeOffset? DeletedAt { get; }
    string? DeletedBy { get; }
    void MarkAsDeleted(string deletedBy);
}
