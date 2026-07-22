namespace Unifesspa.UniPlus.Selecao.Infrastructure.Messaging;

using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Selecao.Domain.Events;

using ProcessoPublicadoAvro = unifesspa.uniplus.selecao.events.ProcessoPublicado;

/// <summary>
/// Handler convention-based do Wolverine que projeta <see cref="ProcessoPublicadoEvent"/>
/// (intra-módulo, PG queue) em <see cref="ProcessoPublicadoAvro"/> (cross-módulo, Kafka)
/// via cascading message — slice canônico do ADR-0005/ADR-0041 (Story #759, T4 #785).
/// </summary>
/// <remarks>
/// Quando o evento é consumido da PG queue <c>domain-events</c> (ADR-0044), Wolverine
/// dispara todos os handlers com assinatura compatível em paralelo —
/// <c>ProcessoPublicadoEventHandler</c> (logging, em Application) e este cascade handler
/// (em Infrastructure). O retorno deste handler é tratado como mensagem encadeada e
/// roteada conforme as regras de <c>PublishMessage&lt;ProcessoPublicadoAvro&gt;()</c> em
/// <c>SelecaoMessagingRegistration</c> (Kafka topic <c>processo_seletivo_events</c> com
/// serializer Confluent SR Avro).
/// <para>
/// O envelope Avro é instalado no outbox dentro da transação do listener da PG queue —
/// se o publish para Kafka falhar, o Wolverine retenta a partir do outbox, garantindo
/// at-least-once para o consumidor cross-módulo.
/// </para>
/// </remarks>
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Convenção do projeto: handlers de cascade de eventos terminam em Handler, ver ProcessoPublicadoEventHandler.")]
public sealed partial class ProcessoPublicadoToKafkaCascadeHandler
{
    public static ProcessoPublicadoAvro Handle(
        ProcessoPublicadoEvent @event,
        ILogger<ProcessoPublicadoToKafkaCascadeHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(logger);

        LogProjetandoParaKafkaCascade(logger, @event.EventId, @event.ProcessoSeletivoId, @event.EditalId);

        return ProcessoPublicadoToAvroMapper.ToAvro(@event);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Projetando ProcessoPublicadoEvent para Kafka cascade. EventoId={EventoId} ProcessoSeletivoId={ProcessoSeletivoId} EditalId={EditalId}")]
    private static partial void LogProjetandoParaKafkaCascade(
        ILogger logger,
        Guid eventoId,
        Guid processoSeletivoId,
        Guid editalId);
}
