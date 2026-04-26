namespace Unifesspa.UniPlus.Infrastructure.Core.Messaging;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

// Inline-qualified Wolverine.IMessageBus para evitar colisão com tipos do
// framework. Wolverine não distingue command de query no nível do bus —
// a separação em IQueryBus/ICommandBus é semântica do projeto (ADR-022) e
// permite que middleware aplique políticas distintas a cada lado do CQRS.
internal sealed class WolverineQueryBus(Wolverine.IMessageBus bus) : IQueryBus
{
    public Task<TResponse> Send<TResponse>(
        IQuery<TResponse> query,
        CancellationToken ct = default)
        => bus.InvokeAsync<TResponse>(query, ct);
}
