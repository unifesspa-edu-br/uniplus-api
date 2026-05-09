namespace Unifesspa.UniPlus.Selecao.Infrastructure.Messaging;

using System.Diagnostics.CodeAnalysis;
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
/// idempotência exigida pela ADR-0014 (consumers deduplicam por <c>EventId</c>).
/// </para>
/// </remarks>
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Convenção do projeto: handlers de cascade de eventos terminam em Handler, ver EditalPublicadoEventHandler.")]
public sealed class EditalPublicadoToKafkaCascadeHandler
{
    public static EditalPublicadoAvro Handle(EditalPublicadoEvent @event)
        => EditalPublicadoToAvroMapper.ToAvro(@event);
}
