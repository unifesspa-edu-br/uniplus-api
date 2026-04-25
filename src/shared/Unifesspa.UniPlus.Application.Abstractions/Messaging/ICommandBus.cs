namespace Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Despacha commands CQRS para seus handlers. É a única abstração que código de aplicação
/// usa para invocar comandos — nunca importar <c>Wolverine.IMessageBus</c> diretamente
/// fora de <c>Infrastructure.Core/Messaging/</c>. Ver ADR-022 (uniplus-docs/docs/adrs/).
/// </summary>
public interface ICommandBus
{
    Task<TResponse> Send<TResponse>(
        ICommand<TResponse> command,
        CancellationToken ct = default);
}
