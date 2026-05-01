namespace Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Despacha queries CQRS para seus handlers. É a única abstração que código de
/// aplicação usa para invocar leituras — nunca importar <c>Wolverine.IMessageBus</c>
/// diretamente fora de <c>Infrastructure.Core/Messaging/</c>. A separação em
/// relação a <see cref="ICommandBus"/> é semântica: bloqueia, na assinatura, que
/// um <see cref="ICommand{TResponse}"/> seja despachado pelo <c>QueryBus</c> ou
/// um <see cref="IQuery{TResponse}"/> pelo <c>CommandBus</c>. Ver ADR-0003.
/// </summary>
public interface IQueryBus
{
    Task<TResponse> Send<TResponse>(
        IQuery<TResponse> query,
        CancellationToken ct = default);
}
