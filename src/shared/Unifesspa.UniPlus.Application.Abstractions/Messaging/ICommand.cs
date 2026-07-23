namespace Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Marker interface para commands CQRS do UniPlus. Identifica intenções de mudança de estado
/// que retornam uma resposta tipada (tipicamente <c>Result&lt;T&gt;</c>). Handlers nunca recebem
/// <see cref="ICommand{TResponse}"/> como tipo genérico — recebem sempre o tipo concreto.
/// Ver ADR-0003.
/// </summary>
public interface ICommand<TResponse>;
