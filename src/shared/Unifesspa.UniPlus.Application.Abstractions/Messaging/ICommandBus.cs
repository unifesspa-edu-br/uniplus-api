namespace Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Despacha commands CQRS para seus handlers. É a única abstração que código de aplicação
/// usa para invocar comandos — nunca importar <c>Wolverine.IMessageBus</c> diretamente
/// fora de <c>Infrastructure.Core/Messaging/</c>. Ver
/// <a href="../../../../../docs/adrs/ADR-022-backbone-cqrs-wolverine.md">ADR-022</a>.
/// </summary>
public interface ICommandBus
{
    Task<TResponse> Send<TResponse>(
        ICommand<TResponse> command,
        CancellationToken ct = default);
}
