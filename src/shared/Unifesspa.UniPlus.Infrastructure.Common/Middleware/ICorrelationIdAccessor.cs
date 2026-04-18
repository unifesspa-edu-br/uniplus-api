namespace Unifesspa.UniPlus.Infrastructure.Common.Middleware;

public interface ICorrelationIdAccessor
{
    string? CorrelationId { get; }

    void SetCorrelationId(string correlationId);
}
