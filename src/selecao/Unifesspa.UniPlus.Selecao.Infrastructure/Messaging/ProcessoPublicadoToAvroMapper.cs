namespace Unifesspa.UniPlus.Selecao.Infrastructure.Messaging;

using System;

using Unifesspa.UniPlus.Kernel.Extensions;
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
    /// <summary>
    /// Com <c>logicalType=timestamp-micros</c>, o Apache.Avro espera <see cref="DateTime"/> e o serializer o converte
    /// para <see cref="long"/> (µs desde o epoch Unix UTC), a precisão com que o instante é gravado; <c>UtcDateTime</c>
    /// garante <c>Kind=Utc</c>. O instante já sai truncado em µs daqui, sem depender de como o serializer trata a
    /// fração abaixo de µs. O <c>SnapshotPublicacaoId</c> é, desde a ADR-0104, o identificador da
    /// <c>VersaoConfiguracao</c> congelada na publicação: o nome do campo, no evento e no schema Avro, é o histórico
    /// que a fila durável já carrega.
    /// </summary>
    public static ProcessoPublicadoAvro ToAvro(ProcessoPublicadoEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);

        return new ProcessoPublicadoAvro
        {
            EventId = evt.EventId.ToString(),
            OccurredOn = evt.OccurredOn.TruncadoEmMicrossegundos().UtcDateTime,
            ProcessoSeletivoId = evt.ProcessoSeletivoId.ToString(),
            EditalId = evt.EditalId.ToString(),
            SnapshotPublicacaoId = evt.SnapshotPublicacaoId.ToString(),
            HashConfiguracao = evt.HashConfiguracao,
            HashEdital = evt.HashEdital,
        };
    }
}
