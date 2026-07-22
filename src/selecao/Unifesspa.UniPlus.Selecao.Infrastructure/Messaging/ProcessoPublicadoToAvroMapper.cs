namespace Unifesspa.UniPlus.Selecao.Infrastructure.Messaging;

using System;

using Unifesspa.UniPlus.Selecao.Domain.Events;

using ProcessoPublicadoAvro = unifesspa.uniplus.selecao.events.ProcessoPublicado;

/// <summary>
/// Conversão entre <see cref="ProcessoPublicadoEvent"/> (record interno do Domain) e
/// <see cref="ProcessoPublicadoAvro"/> (forma wire publicada em Kafka via Confluent SR).
/// </summary>
/// <remarks>
/// Mapping puro sem efeitos colaterais. Posicionado em Infrastructure para não infectar
/// Domain/Application com dependência de Apache.Avro. Executado pelo cascading handler
/// <c>ProcessoPublicadoToKafkaCascadeHandler</c> que dispara quando o evento é consumido
/// na PG queue intra-módulo (ADR-0044).
/// </remarks>
public static class ProcessoPublicadoToAvroMapper
{
    public static ProcessoPublicadoAvro ToAvro(ProcessoPublicadoEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);

        return new ProcessoPublicadoAvro
        {
            EventId = evt.EventId.ToString(),
            // Apache.Avro com logicalType=timestamp-millis espera DateTime; o serializer
            // converte para long (ms desde epoch Unix UTC) automaticamente. UtcDateTime
            // garante Kind=Utc — Apache.Avro arredonda truncando frações de ms.
            OccurredOn = evt.OccurredOn.UtcDateTime,
            ProcessoSeletivoId = evt.ProcessoSeletivoId.ToString(),
            EditalId = evt.EditalId.ToString(),
            // Desde a ADR-0104, o identificador é o da VersaoConfiguracao congelada
            // na publicação; o nome do campo (aqui e no evento) é o histórico, que
            // o schema Avro e a fila durável já carregam.
            SnapshotPublicacaoId = evt.SnapshotPublicacaoId.ToString(),
            HashConfiguracao = evt.HashConfiguracao,
            HashEdital = evt.HashEdital,
        };
    }
}
