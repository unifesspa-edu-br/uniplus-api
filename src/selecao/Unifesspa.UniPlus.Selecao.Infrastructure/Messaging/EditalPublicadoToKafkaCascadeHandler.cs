namespace Unifesspa.UniPlus.Selecao.Infrastructure.Messaging;

using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Selecao.Domain.Events;
using EditalPublicadoAvro = unifesspa.uniplus.selecao.events.EditalPublicado;

/// <summary>
/// Handler convention-based do Wolverine que projeta <see cref="EditalPublicadoEvent"/>
/// (intra-módulo, PG queue) em <see cref="EditalPublicadoAvro"/> (cross-módulo, Kafka)
/// via cascading message.
/// </summary>
/// <remarks>
/// Quando o evento é consumido da PG queue <c>domain-events</c> (ADR-0044), Wolverine
/// dispara todos os handlers com assinatura compatível em paralelo —
/// <c>EditalPublicadoEventHandler</c> (logging, em Application) e este cascade handler
/// (em Infrastructure). O retorno deste handler é tratado como mensagem encadeada e
/// roteada conforme as regras de <c>PublishMessage&lt;EditalPublicadoAvro&gt;()</c> em
/// <c>Program.cs</c> (Kafka topic <c>edital_events</c> com serializer Confluent SR Avro).
/// <para>
/// O envelope Avro é instalado no outbox dentro da transação do listener da PG queue —
/// se o publish para Kafka falhar, o Wolverine retenta a partir do outbox, garantindo
/// at-least-once para o consumidor cross-módulo. A duplicação eventual é tratada pela
/// idempotência exigida pela ADR-0014 (consumers deduplicam por <c>EventoId</c>).
/// </para>
/// <para>
/// <b>Observabilidade do publish para Kafka (issue #427):</b>
/// </para>
/// <list type="bullet">
///   <item>
///     <description>
///       <b>Span OTel built-in do Wolverine</b> (fonte primária): cada publish para Kafka
///       gera um span <c>send</c> com <see cref="System.Diagnostics.ActivityKind.Producer"/>
///       e atributos OpenTelemetry messaging semantic conventions —
///       <c>messaging.system=kafka</c>, <c>messaging.destination=kafka://topic/edital_events</c>,
///       <c>messaging.message_id</c>, <c>messaging.conversation_id</c> (TraceId),
///       <c>messaging.message_type=unifesspa.uniplus.selecao.events.EditalPublicado</c>,
///       <c>messaging.message_payload_size_bytes</c>. Disponível no Grafana Tempo via
///       TraceQL <c>{ name = "send" &amp;&amp; messaging.destination = "kafka://topic/edital_events" }</c>
///       sem instrumentação adicional. Esta é a confirmação canônica de que o publish foi
///       enviado ao broker (o span fecha após o ack do producer Kafka).
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Log estruturado emitido por este handler</b> (fonte complementar):
///       <c>LogProjetandoParaKafkaCascade(EventoId, EditalId, NumeroEdital)</c> em
///       Information com TraceId/SpanId attachados pelo enricher Serilog. Permite query
///       LogQL no Loki <c>{k8s_namespace_name="uniplus"} |= "Projetando EditalPublicadoEvent
///       para Kafka cascade"</c> para alertas baseados em frequência de cascading
///       (ex.: detectar quedas anômalas no volume de publicações).
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Métricas Prometheus</b> (não disponíveis hoje): Wolverine emite métricas OTel
///       (counters/histograms) via <c>OpenTelemetry().WithMetrics()</c>, mas o pipeline
///       <c>otelcol → prometheusremotewrite</c> ainda não está implementado no standalone.
///       Endereçado pela Feature uniplus-infra#242. Até lá, derivação de SLO/SLI usa
///       <c>rate({...} |= "...")</c> sobre o log estruturado deste handler.
///     </description>
///   </item>
/// </list>
/// <para>
/// Em incidente, priorizar Tempo (span semantic conventions) sobre o log: o span tem
/// duração e atributos do broker, o log apenas registra que o cascade handler executou.
/// </para>
/// </remarks>
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Convenção do projeto: handlers de cascade de eventos terminam em Handler, ver EditalPublicadoEventHandler.")]
public sealed partial class EditalPublicadoToKafkaCascadeHandler
{
    public static EditalPublicadoAvro Handle(
        EditalPublicadoEvent @event,
        ILogger<EditalPublicadoToKafkaCascadeHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(logger);

        LogProjetandoParaKafkaCascade(logger, @event.EventId, @event.EditalId, @event.NumeroEdital);

        return EditalPublicadoToAvroMapper.ToAvro(@event);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Projetando EditalPublicadoEvent para Kafka cascade. EventoId={EventoId} EditalId={EditalId} NumeroEdital={NumeroEdital}")]
    private static partial void LogProjetandoParaKafkaCascade(
        ILogger logger,
        Guid eventoId,
        Guid editalId,
        string numeroEdital);
}
