namespace Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Marker interface para queries CQRS do UniPlus. Identifica intenções de leitura
/// que retornam uma resposta tipada (DTO, projeção ou <c>Result&lt;T&gt;</c>) sem
/// efeitos colaterais sobre o estado do domínio. Handlers nunca recebem
/// <see cref="IQuery{TResponse}"/> como tipo genérico — recebem sempre o tipo
/// concreto. A separação em relação a <see cref="ICommand{TResponse}"/> é
/// puramente semântica: protege a leitura de ser despachada por <see cref="ICommandBus"/>
/// (e vice-versa) e permite que middleware aplique políticas distintas a cada
/// lado do CQRS. Ver ADR-0003.
/// </summary>
#pragma warning disable CA1040 // Avoid empty interfaces — este é marker interface intencional do contrato CQRS (ADR-0003).
public interface IQuery<TResponse>;
#pragma warning restore CA1040
