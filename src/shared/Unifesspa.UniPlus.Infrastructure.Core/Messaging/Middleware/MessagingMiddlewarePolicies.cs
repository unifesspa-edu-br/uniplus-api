namespace Unifesspa.UniPlus.Infrastructure.Core.Messaging.Middleware;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

using Wolverine;
using Wolverine.Runtime.Handlers;

/// <summary>
/// Política de aplicação dos middlewares CQRS canônicos do UniPlus
/// (<see cref="WolverineValidationMiddleware"/> e
/// <see cref="WolverineLoggingMiddleware"/>) — registrados apenas em chains
/// cujo tipo de mensagem implemente <see cref="ICommand{TResponse}"/> ou
/// <see cref="IQuery{TResponse}"/>. Mensagens internas do Wolverine (envelopes
/// de outbox, agent commands, system messages) não passam por esse pipeline.
/// </summary>
public static class MessagingMiddlewarePolicies
{
    /// <summary>
    /// Aplica os middlewares de logging e validação ao
    /// <see cref="WolverineOptions"/> do host atual. Deve ser chamado dentro
    /// do callback de <see cref="WolverineOutboxConfiguration.UseWolverineOutboxCascading"/>,
    /// antes do roteamento específico do módulo.
    /// </summary>
    public static void AddCommandQueryMiddleware(this WolverineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Sobrecarga não-genérica: ambas as classes são static (não podem servir
        // de argumento de tipo) — Wolverine descobre os métodos [WolverineBefore]/
        // [WolverineFinally] via reflection sobre o Type passado.
        options.Policies.AddMiddleware(typeof(WolverineLoggingMiddleware), IsCommandOrQueryChain);
        options.Policies.AddMiddleware(typeof(WolverineValidationMiddleware), IsCommandOrQueryChain);
    }

    /// <summary>
    /// Registra o <see cref="CorrelationIdEnvelopeMiddleware"/> em TODOS os chains
    /// Wolverine (commands, queries, eventos cascading, mensagens consumidas de Kafka
    /// que não são command/query). Implementa o terceiro componente da ADR-0052
    /// (rastreabilidade cross-service via <c>uniplus.correlation-id</c>).
    /// </summary>
    /// <remarks>
    /// <para>Aplicado sem filtro <see cref="IsCommandOrQueryChain"/> de propósito:
    /// o <c>CorrelationId</c> precisa fluir também em handlers de eventos publicados
    /// via outbox/Kafka, que não implementam <c>ICommand&lt;T&gt;</c>/<c>IQuery&lt;T&gt;</c>
    /// — caso contrário, o consumer perderia a âncora de negócio assim que o span pai
    /// fosse descartado pelo sampler de 10% em produção (ADR-0018).</para>
    /// <para>Deve ser registrado <em>antes</em> de <see cref="AddCommandQueryMiddleware"/>:
    /// o Wolverine respeita a ordem de registro, e o escopo do <see cref="Serilog.Context.LogContext"/>
    /// precisa estar ativo quando o <see cref="WolverineLoggingMiddleware"/> emite
    /// <c>Processando {RequestName}</c> — caso contrário a entrada inicial sai sem a
    /// propriedade <c>CorrelationId</c>.</para>
    /// </remarks>
    public static void AddCorrelationIdMiddleware(this WolverineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Policies.AddMiddleware(typeof(CorrelationIdEnvelopeMiddleware));
    }

    /// <summary>
    /// Predicado público para reuso em testes e em policies derivadas: identifica
    /// chains cuja mensagem implementa <see cref="ICommand{TResponse}"/> ou
    /// <see cref="IQuery{TResponse}"/>.
    /// </summary>
    public static bool IsCommandOrQueryChain(HandlerChain chain)
    {
        ArgumentNullException.ThrowIfNull(chain);
        return ImplementsOpenGeneric(chain.MessageType, typeof(ICommand<>))
            || ImplementsOpenGeneric(chain.MessageType, typeof(IQuery<>));
    }

    private static bool ImplementsOpenGeneric(Type type, Type openGeneric)
        => Array.Exists(type.GetInterfaces(),
            i => i.IsGenericType && i.GetGenericTypeDefinition() == openGeneric);
}
