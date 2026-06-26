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
    public static void AddCommandQueryMiddleware(this WolverineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Logging estruturado do pipeline CQRS. Sobrecarga não-genérica: a classe é
        // static (não pode servir de argumento de tipo) — Wolverine descobre os
        // métodos [WolverineBefore]/[WolverineFinally] via reflection sobre o Type.
        // Registrado ANTES da validação para que "Processando {RequestName}" saia
        // antes de uma eventual ValidationException curto-circuitar a chain.
        options.Policies.AddMiddleware(typeof(WolverineLoggingMiddleware), IsCommandOrQueryChain);

        // Failure action PII-safe: lança ValidationException SÓ com as falhas de
        // regra, sem serializar o command na mensagem. Registrado ANTES de
        // UseFluentValidation (que usa TryAddSingleton para o default
        // FailureAction<T> — `new ValidationException($"Validation failure on:
        // {message}", failures)`, cujo {message}.ToString() de um record vazaria
        // PII no log via GlobalExceptionMiddleware). Preserva o comportamento do
        // antigo WolverineValidationMiddleware. Ver PiiSafeValidationFailureAction.
        options.Services.TryAddSingleton(typeof(IFailureAction<>), typeof(PiiSafeValidationFailureAction<>));

        // Validação FluentValidation idiomática do Wolverine: gera middleware
        // tipado por mensagem (IValidator<T> / IEnumerable<IValidator<T>> injetados
        // pelo codegen, sem service location de IServiceProvider) — forward-compat
        // com a ServiceLocationPolicy.NotAllowed que vira default no Wolverine 6.0.
        // Lança a mesma FluentValidation.ValidationException, mapeada como 422 pelo
        // GlobalExceptionMiddleware. ExplicitRegistration: os validators continuam
        // vindo dos AddValidatorsFromAssembly de cada módulo (o pacote não
        // redescobre nem re-registra). Aplica-se só a chains com validator — no
        // UniPlus, exclusivamente commands/queries.
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
        => Array.Exists(type.GetInterfaces(),
            i => i.IsGenericType && i.GetGenericTypeDefinition() == openGeneric);
}
