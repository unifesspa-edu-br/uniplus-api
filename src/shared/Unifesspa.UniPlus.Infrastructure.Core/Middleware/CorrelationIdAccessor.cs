namespace Unifesspa.UniPlus.Infrastructure.Core.Middleware;

public sealed class CorrelationIdAccessor : ICorrelationIdWriter
{
    // AsyncLocal estático de propósito: o isolamento vem do ExecutionContext,
    // não da instância do accessor. Permite registro Singleton no DI sem risco
    // de cross-talk entre requests — cada request roda em seu próprio fluxo
    // lógico e o AsyncLocal.Value é copiado/restaurado automaticamente.
    private static readonly AsyncLocal<string?> Current = new();

    public string? CorrelationId => Current.Value;

    public void SetCorrelationId(string correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        Current.Value = correlationId;
    }
}
