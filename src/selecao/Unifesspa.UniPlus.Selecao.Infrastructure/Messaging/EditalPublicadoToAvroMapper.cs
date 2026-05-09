namespace Unifesspa.UniPlus.Selecao.Infrastructure.Messaging;

using System;
using Unifesspa.UniPlus.Selecao.Domain.Events;
using EditalPublicadoAvro = unifesspa.uniplus.selecao.events.EditalPublicado;

/// <summary>
/// Conversão entre <see cref="EditalPublicadoEvent"/> (record interno do Domain) e
/// <see cref="EditalPublicadoAvro"/> (forma wire publicada em Kafka via Confluent SR).
/// </summary>
/// <remarks>
/// Mapping puro sem efeitos colaterais. Posicionado em Infrastructure para não infectar
/// Domain/Application com dependência de Apache.Avro. Executado pelo cascading handler
/// <c>EditalPublicadoToKafkaCascadeHandler</c> que dispara quando o evento é consumido
/// na PG queue intra-módulo (ADR-0044).
/// </remarks>
public static class EditalPublicadoToAvroMapper
{
    public static EditalPublicadoAvro ToAvro(EditalPublicadoEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);

        return new EditalPublicadoAvro
        {
            EventId = evt.EventId.ToString(),
            // Apache.Avro com logicalType=timestamp-millis espera DateTime; o serializer
            // converte para long (ms desde epoch Unix UTC) automaticamente. UtcDateTime
            // garante Kind=Utc — Apache.Avro arredonda truncando frações de ms.
            OccurredOn = evt.OccurredOn.UtcDateTime,
            EditalId = evt.EditalId.ToString(),
            NumeroEdital = evt.NumeroEdital,
        };
    }
}
