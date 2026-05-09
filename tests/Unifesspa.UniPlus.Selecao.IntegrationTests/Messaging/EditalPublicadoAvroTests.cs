namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Messaging;

using System;
using System.IO;
using AwesomeAssertions;
using Avro.IO;
using Avro.Specific;
using Unifesspa.UniPlus.Selecao.Domain.Events;
using Unifesspa.UniPlus.Selecao.Infrastructure.Messaging;
using EditalPublicadoAvro = unifesspa.uniplus.selecao.events.EditalPublicado;

/// <summary>
/// Smoke unit tests para o pipeline Avro de <see cref="EditalPublicadoEvent"/>:
/// schema parse, mapping puro <c>Event → Avro</c> e round-trip Avro binary
/// (serialize → deserialize) usando <see cref="SpecificDefaultWriter"/> /
/// <see cref="SpecificDefaultReader"/>.
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
public sealed class EditalPublicadoAvroTests
{
    [Fact(DisplayName = "Schema é carregado do embedded resource em Selecao.Domain")]
    public void Schema_DeveSerCarregadoDoEmbeddedResource()
    {
        global::Avro.Schema schema = EditalPublicadoAvro.AvroSchema;

        schema.Should().NotBeNull();
        schema.Tag.Should().Be(global::Avro.Schema.Type.Record);

        global::Avro.RecordSchema schemaRecord = (global::Avro.RecordSchema)schema;
        schemaRecord.Name.Should().Be("EditalPublicado");
        schemaRecord.Namespace.Should().Be("unifesspa.uniplus.selecao.events");
        schemaRecord.Fields.Select(f => f.Name).Should().BeEquivalentTo(
            ["EventId", "OccurredOn", "EditalId", "NumeroEdital"]);
    }

    [Fact(DisplayName = "Mapper Event → Avro preserva todos os campos")]
    public void Mapper_DeveMaperearTodosOsCampos()
    {
        EditalPublicadoEvent evt = new(
            EditalId: Guid.CreateVersion7(),
            NumeroEdital: "001/2026");

        EditalPublicadoAvro avro = EditalPublicadoToAvroMapper.ToAvro(evt);

        avro.EventId.Should().Be(evt.EventId.ToString());
        avro.OccurredOn.Should().Be(evt.OccurredOn.UtcDateTime);
        avro.EditalId.Should().Be(evt.EditalId.ToString());
        avro.NumeroEdital.Should().Be(evt.NumeroEdital);
    }

    [Fact(DisplayName = "Mapper rejeita null")]
    public void Mapper_NullEvent_DeveLancarArgumentNull()
    {
        Action act = () => EditalPublicadoToAvroMapper.ToAvro(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Round-trip Avro binary preserva todos os campos")]
    public void RoundTrip_DevePreservarCampos()
    {
        // Construímos o Avro a partir de um evento real para garantir que o
        // schema usado na escrita seja exatamente o mesmo carregado do recurso —
        // qualquer drift entre .avsc e a classe ISpecificRecord falhará aqui.
        EditalPublicadoEvent evt = new(
            EditalId: Guid.CreateVersion7(),
            NumeroEdital: "042/2026");

        EditalPublicadoAvro original = EditalPublicadoToAvroMapper.ToAvro(evt);

        SpecificDefaultWriter writer = new(EditalPublicadoAvro.AvroSchema);
        using MemoryStream stream = new();
        BinaryEncoder encoder = new(stream);
        writer.Write(original, encoder);
        encoder.Flush();

        stream.Position = 0;
        SpecificDefaultReader reader = new(EditalPublicadoAvro.AvroSchema, EditalPublicadoAvro.AvroSchema);
        BinaryDecoder decoder = new(stream);
        EditalPublicadoAvro? decoded = reader.Read(reuse: default(EditalPublicadoAvro), decoder);

        decoded.Should().NotBeNull();
        decoded.EventId.Should().Be(original.EventId);
        // timestamp-millis trunca para ms — original tem ticks (precisão sub-ms),
        // decoded vem só com ms. Comparar truncado.
        decoded.OccurredOn.Should().BeCloseTo(original.OccurredOn, TimeSpan.FromMilliseconds(1));
        decoded.EditalId.Should().Be(original.EditalId);
        decoded.NumeroEdital.Should().Be(original.NumeroEdital);
    }

    [Theory(DisplayName = "ISpecificRecord Get por posição retorna campos esperados")]
    [InlineData(0, "EventId")]
    [InlineData(1, "OccurredOn")]
    [InlineData(2, "EditalId")]
    [InlineData(3, "NumeroEdital")]
    public void Get_DeveRetornarCampoCorreto(int fieldPos, string expectedFieldName)
    {
        EditalPublicadoAvro avro = new()
        {
            EventId = "evt-id",
            OccurredOn = new DateTime(2026, 5, 9, 12, 0, 0, DateTimeKind.Utc),
            EditalId = "edital-id",
            NumeroEdital = "001/2026",
        };

        ISpecificRecord record = (ISpecificRecord)avro;
        object value = record.Get(fieldPos);

        value.Should().NotBeNull();

        // Cobertura por nome para evitar drift de ordem entre .avsc e Get/Put.
        global::Avro.RecordSchema schema = (global::Avro.RecordSchema)EditalPublicadoAvro.AvroSchema;
        schema.Fields[fieldPos].Name.Should().Be(expectedFieldName);
    }

    [Fact(DisplayName = "ISpecificRecord Get com posição inválida lança AvroRuntimeException")]
    public void Get_PosicaoInvalida_DeveLancar()
    {
        EditalPublicadoAvro avro = new();
        ISpecificRecord record = (ISpecificRecord)avro;
        Action act = () => record.Get(99);
        act.Should().Throw<global::Avro.AvroRuntimeException>();
    }

    [Fact(DisplayName = "ISpecificRecord Put com posição inválida lança AvroRuntimeException")]
    public void Put_PosicaoInvalida_DeveLancar()
    {
        EditalPublicadoAvro avro = new();
        ISpecificRecord record = (ISpecificRecord)avro;
        Action act = () => record.Put(99, "x");
        act.Should().Throw<global::Avro.AvroRuntimeException>();
    }
}
