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
