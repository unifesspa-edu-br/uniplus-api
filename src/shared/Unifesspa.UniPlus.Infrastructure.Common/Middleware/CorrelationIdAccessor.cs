namespace Unifesspa.UniPlus.Infrastructure.Common.Middleware;

public sealed class CorrelationIdAccessor : ICorrelationIdAccessor
{
    private static readonly AsyncLocal<string?> _current = new();

    public string? CorrelationId => _current.Value;

    public void SetCorrelationId(string correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        _current.Value = correlationId;
    }
}
