namespace Unifesspa.UniPlus.Kernel.Domain.Interfaces;

public interface IAuditableEntity
{
    DateTimeOffset CreatedAt { get; }
    DateTimeOffset? UpdatedAt { get; }
    string? CreatedBy { get; }
    string? UpdatedBy { get; }
}
