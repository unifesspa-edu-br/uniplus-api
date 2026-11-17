namespace Unifesspa.UniPlus.Infrastructure.Core.Messaging.Middleware;

using Microsoft.Extensions.DependencyInjection.Extensions;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

using Wolverine;
using Wolverine.FluentValidation;
using Wolverine.Runtime.Handlers;

/// <summary>
/// Política de aplicação dos middlewares CQRS canônicos do UniPlus
/// (<see cref="WolverineLoggingMiddleware"/> + validação FluentValidation
/// idiomática via <c>UseFluentValidation</c>). O logging é registrado apenas em
/// chains cujo tipo de mensagem implemente <see cref="ICommand{TResponse}"/> ou
/// <see cref="IQuery{TResponse}"/>; a validação é aplicada pelo Wolverine a toda
/// chain que tenha um <see cref="FluentValidation.IValidator{T}"/> registrado
/// (no UniPlus, somente commands/queries têm validator). Mensagens internas do
/// Wolverine (envelopes de outbox, agent commands, system messages) não passam
/// por logging nem têm validator, então ficam fora desse pipeline.
/// </summary>
public static class MessagingMiddlewarePolicies
{
    /// <summary>
    /// Aplica os middlewares de logging e validação ao
    /// <see cref="WolverineOptions"/> do host atual. Deve ser chamado dentro
    /// do callback de <see cref="WolverineOutboxConfiguration.UseWolverineOutboxCascading"/>,
    /// antes do roteamento específico do módulo.
    /// </summary>
    /// <remarks>
    /// <para>A ordem das três instalações é contrato.</para>
    /// <para><strong>Logging.</strong> Usa a sobrecarga não genérica de <c>AddMiddleware</c>, porque a classe é
    /// estática e não serve de argumento de tipo; o Wolverine descobre os métodos
    /// <c>[WolverineBefore]</c>/<c>[WolverineFinally]</c> por reflexão sobre o <see cref="Type"/>. Vem antes da
    /// validação para que "Processando {RequestName}" saia antes de uma <c>ValidationException</c> interromper a chain.</para>
    /// <para><strong>Ação de falha da validação.</strong> Lança <c>ValidationException</c> só com as falhas de regra,
    /// sem serializar o command na mensagem. Precisa ser registrada antes de <c>UseFluentValidation</c>, que usa
    /// <c>TryAddSingleton</c> para o <c>FailureAction&lt;T&gt;</c> padrão — e o <c>ToString()</c> de um record no
    /// <c>{message}</c> dessa ação padrão vazaria dado pessoal no log pelo <c>GlobalExceptionMiddleware</c>.
    /// Ver <see cref="PiiSafeValidationFailureAction{T}"/>.</para>
    /// <para><strong>Validação.</strong> O Wolverine gera middleware tipado por mensagem (<c>IValidator&lt;T&gt;</c>
    /// injetado pelo codegen, sem service location), compatível com a <c>ServiceLocationPolicy.NotAllowed</c> que
    /// vira padrão no Wolverine 6.0. Lança a mesma <c>ValidationException</c>, mapeada como 422 pelo
    /// <c>GlobalExceptionMiddleware</c>. Com <c>ExplicitRegistration</c>, os validators continuam vindo dos
    /// <c>AddValidatorsFromAssembly</c> de cada módulo. Aplica-se só a chains com validator, que no Uni+ são
    /// exclusivamente commands e queries.</para>
    /// </remarks>
    public static void AddCommandQueryMiddleware(this WolverineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Policies.AddMiddleware(typeof(WolverineLoggingMiddleware), IsCommandOrQueryChain);
        options.Services.TryAddSingleton(typeof(IFailureAction<>), typeof(PiiSafeValidationFailureAction<>));
        options.UseFluentValidation(RegistrationBehavior.ExplicitRegistration);
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
        => Array.Exists(
            type.GetInterfaces(),
            interfaceImplementada => interfaceImplementada.IsGenericType
                && interfaceImplementada.GetGenericTypeDefinition() == openGeneric);
}
