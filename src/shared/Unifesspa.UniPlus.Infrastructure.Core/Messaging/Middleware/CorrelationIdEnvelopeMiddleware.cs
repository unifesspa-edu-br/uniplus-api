namespace Unifesspa.UniPlus.Infrastructure.Core.Messaging.Middleware;

using System.Diagnostics;
using System.Text.RegularExpressions;

using Serilog.Context;

using Unifesspa.UniPlus.Infrastructure.Core.Middleware;

using Wolverine;
using Wolverine.Attributes;

/// <summary>
/// Middleware Wolverine que propaga <c>CorrelationId</c> ponta a ponta em
/// mensagens trocadas pelo backbone CQRS (commands, queries, eventos
/// publicados via outbox/Kafka). Implementa o terceiro componente da ADR-0052
/// — taparia a lacuna em que o <c>X-Correlation-Id</c> HTTP morre no boundary
/// Kafka, deixando o consumer sem âncora de negócio quando o span pai é
/// descartado pelo sampling head-based de 10% em produção.
/// </summary>
/// <remarks>
/// <para><strong>Forma simétrica produtor ↔ consumidor.</strong>
/// O middleware roda no <see cref="WolverineBeforeAttribute"/> de TODOS os chains
/// Wolverine (registrado por <see cref="MessagingMiddlewarePolicies.AddCorrelationIdMiddleware"/>),
/// antes do <see cref="WolverineLoggingMiddleware"/>:</para>
/// <list type="number">
///   <item><description>Lê <c>uniplus.correlation-id</c> dos headers do envelope incoming.</description></item>
///   <item><description>Valida com a mesma regex de <see cref="CorrelationIdMiddleware"/>
///   (<c>[A-Za-z0-9\-_.]{1,128}</c>) — rejeita input malformado para evitar log injection
///   e poluição de dashboards downstream. Em ausência ou inválido, gera um novo GUID.</description></item>
///   <item><description>Reescreve <c>envelope.Headers</c> com o valor canônico — assim
///   <see cref="WolverineOutboxConfiguration"/> consegue propagá-lo automaticamente para
///   qualquer outgoing message do handler (cascading, <c>IMessageBus.PublishAsync</c>,
///   etc.) via <see cref="IPolicies.PropagateIncomingHeaderToOutgoing(string)"/>.</description></item>
///   <item><description>Faz <see cref="LogContext.PushProperty(string, object?, bool)"/>
///   da propriedade Serilog <c>CorrelationId</c> e retorna o <see cref="IDisposable"/>;
///   o code-gen do Wolverine captura a variável local e a passa para
///   <c>Finally</c>, que dispõe o escopo ao final do handler.</description></item>
///   <item><description>Propaga também para <see cref="Activity.Current"/> como tag
///   <c>correlation_id</c> — fecha o ciclo log ↔ trace no Grafana (Loki
///   <c>derivedFields</c> → Tempo), mesmo quando o span pai foi descartado pelo
///   sampler de produção (ADR-0018).</description></item>
/// </list>
/// <para><strong>Por que não OTel Baggage?</strong> Baggage só sobrevive enquanto o
/// <c>ActivityContext</c> sobrevive — sob sampling agressivo o contexto pode ser
/// descartado. <c>CorrelationId</c> é dado de negócio independente do trace técnico
/// (oncall lê o GUID em um e-mail do candidato antes de abrir o Grafana). Explicitar
/// o header Wolverine deixa o contrato claro no código sem dependência implícita do SDK.</para>
/// <para><strong>Why not <see cref="Envelope.CorrelationId"/> built-in?</strong> O campo
/// existe no Wolverine, mas o ADR-0052 (Opção A') optou pelo header explícito
/// <c>uniplus.correlation-id</c> propagado via
/// <see cref="IPolicies.PropagateIncomingHeaderToOutgoing(string)"/> — wire format
/// uniforme em qualquer broker, sem depender da serialização específica do transport
/// (Kafka, PG queue, gRPC). Drift entre o built-in e a property Serilog
/// <c>CorrelationId</c> deixa de existir porque há apenas um valor canônico.</para>
/// </remarks>
public static partial class CorrelationIdEnvelopeMiddleware
{
    /// <summary>
    /// Nome do header propagado em envelopes Wolverine (outbox → Kafka, PG queue, gRPC etc.).
    /// Prefixo <c>uniplus.</c> deixa explícito que é um header de domínio do projeto,
    /// fora do espaço W3C / OTel.
    /// </summary>
    public const string HeaderName = "uniplus.correlation-id";

    /// <summary>Limite máximo do correlation id — espelha <see cref="CorrelationIdMiddleware.MaxCorrelationIdLength"/>.</summary>
    public const int MaxCorrelationIdLength = 128;

    /// <summary>
    /// Nome da propriedade Serilog <c>CorrelationId</c> empurrada via
    /// <see cref="LogContext.PushProperty(string, object?, bool)"/>. Reusa a mesma string
    /// usada pelo middleware HTTP <see cref="CorrelationIdMiddleware.LogContextProperty"/> —
    /// permite que queries LogQL <c>{CorrelationId="..."}</c> reconstruam fluxos sem ramificar.
    /// </summary>
    public const string LogContextProperty = CorrelationIdMiddleware.LogContextProperty;

    /// <summary>
    /// Nome do span attribute (Activity tag) — espelha
    /// <see cref="CorrelationIdMiddleware.ActivityTagName"/> para drill-down Loki → Tempo
    /// uniforme entre fluxos HTTP-only e fluxos que atravessam Kafka.
    /// </summary>
    public const string ActivityTagName = CorrelationIdMiddleware.ActivityTagName;

    /// <summary>
    /// Hook executado antes do handler. Garante o invariante de propagação:
    /// (1) header canônico no envelope; (2) tag no <see cref="Activity.Current"/>;
    /// (3) escopo <see cref="LogContext"/> da propriedade <c>CorrelationId</c>.
    /// </summary>
    /// <remarks>
    /// <para><strong>Ordem de precedência canônica</strong> para resolver o
    /// <c>CorrelationId</c> de um handler:</para>
    /// <list type="number">
    ///   <item><description><b>Header explícito</b> <c>uniplus.correlation-id</c> no envelope —
    ///   fluxo cross-host (Kafka via outbox): o produtor já gerou/validou e o consumer
    ///   preserva identicamente. Validado contra <c>CorrelationIdMiddleware.FormatoValidoPattern</c>.</description></item>
    ///   <item><description><b>Ambient via <see cref="ICorrelationIdAccessor"/></b> (AsyncLocal
    ///   populado pelo <see cref="CorrelationIdMiddleware"/> HTTP) — fluxo in-process:
    ///   controller chama <c>ICommandBus.Send</c>, Wolverine cria envelope local sem o header
    ///   mas o AsyncLocal flui através do <c>await</c>. Submetido à mesma validação regex.</description></item>
    ///   <item><description><b>GUID novo</b> (formato <c>"D"</c>) — último recurso para handlers
    ///   sem origem HTTP nem header de Kafka (scheduler interno, retry de mensagem que perdeu
    ///   o header). Garante que todo handler emita logs com pelo menos um id estável.</description></item>
    /// </list>
    /// </remarks>
    /// <param name="envelope">Envelope Wolverine do incoming — fornecido pelo code-gen.</param>
    /// <param name="accessor"><see cref="ICorrelationIdAccessor"/> resolvido via DI pelo
    /// code-gen Wolverine. Registrado por
    /// <c>CorrelationIdServiceCollectionExtensions.AddCorrelationIdAccessor</c> em cada
    /// <c>Program.cs</c>.</param>
    /// <returns>O escopo do <see cref="LogContext.PushProperty(string, object?, bool)"/>;
    /// o code-gen do Wolverine captura como variável local e o <c>Finally</c> dispõe ao
    /// término do handler (sucesso ou falha). Pattern espelhado do
    /// <see cref="WolverineLoggingMiddleware"/>.</returns>
    [WolverineBefore]
    public static IDisposable Before(Envelope envelope, ICorrelationIdAccessor accessor)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(accessor);

        string correlationId = ObterOuGerarCorrelationId(envelope, accessor);

        // Reescreve sempre — assim, mesmo quando o header chegou ausente ou inválido,
        // PropagateIncomingHeaderToOutgoing repassa o GUID gerado aqui para qualquer
        // cascading message saída do handler. Sem isso, traços que entram pela primeira
        // vez no sistema (ex.: HTTP → ICommandBus.Send → cascading) sairiam sem o header.
        envelope.Headers[HeaderName] = correlationId;

        // Activity.Current pode ser null quando OTel não está wired (suites de teste
        // HTTP-only com Observability:Enabled=false). O ?. mantém o middleware safe.
        Activity.Current?.SetTag(ActivityTagName, correlationId);

        return LogContext.PushProperty(LogContextProperty, correlationId);
    }

    /// <summary>
    /// Hook executado sempre ao final do handler (sucesso ou falha). Dispõe o escopo
    /// do <see cref="LogContext"/> abriado em <see cref="Before"/>, evitando vazamento
    /// da propriedade <c>CorrelationId</c> entre execuções consecutivas no mesmo thread.
    /// </summary>
    /// <param name="scope">O <see cref="IDisposable"/> retornado por <see cref="Before"/>,
    /// capturado pelo code-gen do Wolverine por correspondência de tipo na chain.</param>
    [WolverineFinally]
    public static void Finally(IDisposable scope)
    {
        // Guard contra contrato quebrado do code-gen Wolverine — Before sempre retorna
        // não-null. Se o framework regredir, queremos a falha cedo e tipada (CA1062).
        ArgumentNullException.ThrowIfNull(scope);

        scope.Dispose();
    }

    private static string ObterOuGerarCorrelationId(Envelope envelope, ICorrelationIdAccessor accessor)
    {
        // 1. Header explícito no envelope é a fonte canônica para fluxos cross-host
        //    (consumer recebendo de Kafka via outbox). O valor já foi gerado/validado
        //    pelo produtor — preservamos identicamente.
        if (envelope.Headers.TryGetValue(HeaderName, out string? valor)
            && !string.IsNullOrEmpty(valor)
            && FormatoValido().IsMatch(valor))
        {
            return valor;
        }

        // 2. Fallback ambient para fluxos in-process iniciados por HTTP: controller chama
        //    ICommandBus.Send → Wolverine cria envelope local sem o header (WolverineCommandBus
        //    delega ao InvokeAsync sem DeliveryOptions). O CorrelationIdMiddleware HTTP
        //    populou o AsyncLocal do CorrelationIdAccessor antes do Send; preservamos esse
        //    valor para que o handler emita logs no mesmo id que o request original.
        //    Sem isto, geraríamos um segundo GUID e sobrescreveríamos o LogContext do HTTP,
        //    quebrando o drill-down end-to-end.
        string? ambient = accessor.CorrelationId;
        if (!string.IsNullOrEmpty(ambient) && FormatoValido().IsMatch(ambient))
        {
            return ambient;
        }

        // 3. Último recurso: handler invocado sem origem HTTP nem header de Kafka
        //    (ex.: scheduler interno, retry de mensagem que perdeu o header). Gera GUID
        //    novo para que o handler tenha pelo menos um id estável durante sua execução.
        return Guid.NewGuid().ToString("D");
    }

    // Pattern consumido também por CorrelationIdMiddleware (HTTP) via
    // CorrelationIdMiddleware.FormatoValidoPattern — single source of truth garante
    // que ambos os boundaries (HTTP e Kafka) validem com a MESMA regra. Sem isto,
    // refactor em um lado deixaria drift silencioso no wire format do uniplus.correlation-id.
    [GeneratedRegex(CorrelationIdMiddleware.FormatoValidoPattern)]
    private static partial Regex FormatoValido();
}
