namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Messaging;

using System;
using System.IO;

using Avro.IO;
using Avro.Specific;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Events;
using Unifesspa.UniPlus.Selecao.Infrastructure.Messaging;

using ProcessoPublicadoAvro = unifesspa.uniplus.selecao.events.ProcessoPublicado;

/// <summary>
/// Smoke unit tests para o pipeline Avro de <see cref="ProcessoPublicadoEvent"/>
/// (Story #759, T4 #785): schema parse, mapping puro <c>Event → Avro</c> e
/// round-trip Avro binary (serialize → deserialize) usando
/// <see cref="SpecificDefaultWriter"/> / <see cref="SpecificDefaultReader"/>.
/// </summary>
/// <remarks>
/// Não toca rede nem Schema Registry — exercita apenas a camada de wire form.
/// O caminho cross-process com Confluent SR (header magic byte + schema id +
/// payload) é testado em integração quando o ambiente fornece Apicurio
/// (compose local ou cluster standalone).
/// </remarks>
[Trait("Category", "Unit")]
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1859:Use concrete types when possible for improved performance",
    Justification = "Testes do contrato ISpecificRecord — uso da interface é intencional para verificar a implementação dinâmica do Apache.Avro.")]
public sealed class ProcessoPublicadoAvroTests
{
    [Fact(DisplayName = "Schema é carregado do embedded resource em Selecao.Domain")]
    public void Schema_DeveSerCarregadoDoEmbeddedResource()
    {
        global::Avro.Schema schema = ProcessoPublicadoAvro.AvroSchema;

        schema.Should().NotBeNull();
        schema.Tag.Should().Be(global::Avro.Schema.Type.Record);

        global::Avro.RecordSchema schemaRecord = (global::Avro.RecordSchema)schema;
        schemaRecord.Name.Should().Be("ProcessoPublicado");
        schemaRecord.Namespace.Should().Be("unifesspa.uniplus.selecao.events");
        schemaRecord.Fields.Select(f => f.Name).Should().BeEquivalentTo(
            ["EventId", "OccurredOn", "ProcessoSeletivoId", "EditalId", "SnapshotPublicacaoId", "HashConfiguracao", "HashEdital"]);
    }

    [Fact(DisplayName = "Mapper Event → Avro preserva todos os campos")]
    public void Mapper_DeveMaperearTodosOsCampos()
    {
        ProcessoPublicadoEvent evt = new(
            ProcessoSeletivoId: Guid.CreateVersion7(),
            EditalId: Guid.CreateVersion7(),
            SnapshotPublicacaoId: Guid.CreateVersion7(),
            HashConfiguracao: new string('a', 64),
            HashEdital: new string('b', 64),
            OccurredOn: new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero));

        ProcessoPublicadoAvro avro = ProcessoPublicadoToAvroMapper.ToAvro(evt);

        avro.EventId.Should().Be(evt.EventId.ToString());
        avro.OccurredOn.Should().Be(evt.OccurredOn.UtcDateTime);
        avro.ProcessoSeletivoId.Should().Be(evt.ProcessoSeletivoId.ToString());
        avro.EditalId.Should().Be(evt.EditalId.ToString());
        // O campo wire preserva o nome histórico (compat BACKWARD do Schema
        // Registry) — o mapper faz a ponte entre os dois vocabulários.
        avro.SnapshotPublicacaoId.Should().Be(evt.SnapshotPublicacaoId.ToString());
        avro.HashConfiguracao.Should().Be(evt.HashConfiguracao);
        avro.HashEdital.Should().Be(evt.HashEdital);
    }

    [Fact(DisplayName = "Mapper rejeita null")]
    public void Mapper_NullEvent_DeveLancarArgumentNull()
    {
        Action act = () => ProcessoPublicadoToAvroMapper.ToAvro(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Round-trip Avro binary preserva todos os campos")]
    public void RoundTrip_DevePreservarCampos()
    {
        // Construímos o Avro a partir de um evento real para garantir que o
        // schema usado na escrita seja exatamente o mesmo carregado do recurso —
        // qualquer drift entre .avsc e a classe ISpecificRecord falhará aqui.
        ProcessoPublicadoEvent evt = new(
            ProcessoSeletivoId: Guid.CreateVersion7(),
            EditalId: Guid.CreateVersion7(),
            SnapshotPublicacaoId: Guid.CreateVersion7(),
            HashConfiguracao: new string('c', 64),
            HashEdital: new string('d', 64),
            OccurredOn: new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero));

        ProcessoPublicadoAvro original = ProcessoPublicadoToAvroMapper.ToAvro(evt);

        SpecificDefaultWriter writer = new(ProcessoPublicadoAvro.AvroSchema);
        using MemoryStream stream = new();
        BinaryEncoder encoder = new(stream);
        writer.Write(original, encoder);
        encoder.Flush();

        stream.Position = 0;
        SpecificDefaultReader reader = new(ProcessoPublicadoAvro.AvroSchema, ProcessoPublicadoAvro.AvroSchema);
        BinaryDecoder decoder = new(stream);
        ProcessoPublicadoAvro? decoded = reader.Read(reuse: default(ProcessoPublicadoAvro), decoder);

        decoded.Should().NotBeNull();
        decoded.EventId.Should().Be(original.EventId);
        // timestamp-millis trunca para ms — original tem ticks (precisão sub-ms),
        // decoded vem só com ms. Comparar truncado.
        decoded.OccurredOn.Should().BeCloseTo(original.OccurredOn, TimeSpan.FromMilliseconds(1));
        decoded.ProcessoSeletivoId.Should().Be(original.ProcessoSeletivoId);
        decoded.EditalId.Should().Be(original.EditalId);
        decoded.SnapshotPublicacaoId.Should().Be(original.SnapshotPublicacaoId);
        decoded.HashConfiguracao.Should().Be(original.HashConfiguracao);
        decoded.HashEdital.Should().Be(original.HashEdital);
    }

    [Theory(DisplayName = "ISpecificRecord Get por posição retorna campos esperados")]
    [InlineData(0, "EventId")]
    [InlineData(1, "OccurredOn")]
    [InlineData(2, "ProcessoSeletivoId")]
    [InlineData(3, "EditalId")]
    [InlineData(4, "SnapshotPublicacaoId")]
    [InlineData(5, "HashConfiguracao")]
    [InlineData(6, "HashEdital")]
    public void Get_DeveRetornarCampoCorreto(int fieldPos, string expectedFieldName)
    {
        ProcessoPublicadoAvro avro = new()
        {
            EventId = "evt-id",
            OccurredOn = new DateTime(2026, 5, 9, 12, 0, 0, DateTimeKind.Utc),
            ProcessoSeletivoId = "processo-id",
            EditalId = "edital-id",
            SnapshotPublicacaoId = "snapshot-id",
            HashConfiguracao = new string('a', 64),
            HashEdital = new string('b', 64),
        };

        ISpecificRecord record = (ISpecificRecord)avro;
        object value = record.Get(fieldPos);

        value.Should().NotBeNull();

        // Cobertura por nome para evitar drift de ordem entre .avsc e Get/Put.
        global::Avro.RecordSchema schema = (global::Avro.RecordSchema)ProcessoPublicadoAvro.AvroSchema;
        schema.Fields[fieldPos].Name.Should().Be(expectedFieldName);
    }

    [Fact(DisplayName = "ISpecificRecord Get com posição inválida lança AvroRuntimeException")]
    public void Get_PosicaoInvalida_DeveLancar()
    {
        ProcessoPublicadoAvro avro = new();
        ISpecificRecord record = (ISpecificRecord)avro;
        Action act = () => record.Get(99);
        act.Should().Throw<global::Avro.AvroRuntimeException>();
    }

    [Fact(DisplayName = "ISpecificRecord Put com posição inválida lança AvroRuntimeException")]
    public void Put_PosicaoInvalida_DeveLancar()
    {
        ProcessoPublicadoAvro avro = new();
        ISpecificRecord record = (ISpecificRecord)avro;
        Action act = () => record.Put(99, "x");
        act.Should().Throw<global::Avro.AvroRuntimeException>();
    }
}
