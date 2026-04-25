namespace Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Marker interface para commands CQRS do UniPlus. Identifica intenções de mudança de estado
/// que retornam uma resposta tipada (tipicamente <c>Result&lt;T&gt;</c>). Handlers nunca recebem
/// <see cref="ICommand{TResponse}"/> como tipo genérico — recebem sempre o tipo concreto.
/// Ver <a href="../../../../../docs/adrs/ADR-022-backbone-cqrs-wolverine.md">ADR-022</a>.
/// </summary>
#pragma warning disable CA1040 // Avoid empty interfaces — este é marker interface intencional do contrato CQRS (ADR-022).
public interface ICommand<TResponse>;
#pragma warning restore CA1040
