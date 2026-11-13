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

    // O campo SnapshotPublicacaoId preserva o nome histórico do schema (compatibilidade BACKWARD do Schema Registry): o
    // mapper faz a ponte entre os dois vocabulários.
    [Fact(DisplayName = "Mapper Event → Avro preserva todos os campos")]
    public void Mapper_DeveMaperearTodosOsCampos()
    {
        ProcessoPublicadoEvent evt = EventoEm(new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero));

        ProcessoPublicadoAvro avro = ProcessoPublicadoToAvroMapper.ToAvro(evt);

        avro.Should().BeEquivalentTo(new
        {
            EventId = evt.EventId.ToString(),
            OccurredOn = evt.OccurredOn.UtcDateTime,
            ProcessoSeletivoId = evt.ProcessoSeletivoId.ToString(),
            EditalId = evt.EditalId.ToString(),
            SnapshotPublicacaoId = evt.SnapshotPublicacaoId.ToString(),
            evt.HashConfiguracao,
            evt.HashEdital,
        });
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
        // O Avro vem de um evento real, para que a escrita use o schema carregado do recurso: qualquer divergência entre
        // o .avsc e a classe ISpecificRecord falha aqui.
        ProcessoPublicadoAvro original = ProcessoPublicadoToAvroMapper.ToAvro(
            EventoEm(new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero)));

        ProcessoPublicadoAvro decoded = IdaEVolta(original);

        decoded.Should().BeEquivalentTo(original);
    }

    [Fact(DisplayName = "O instante com 7 casas volta do Avro com 6, em UTC")]
    public void RoundTrip_InstanteComSeteCasas_VoltaEmMicrossegundos()
    {
        DateTimeOffset instante = new DateTimeOffset(2026, 11, 13, 10, 0, 0, TimeSpan.FromHours(-3)).AddTicks(1_234_567);

        ProcessoPublicadoAvro decoded = IdaEVolta(ProcessoPublicadoToAvroMapper.ToAvro(EventoEm(instante)));

        decoded.OccurredOn.Should().Be(new DateTime(2026, 11, 13, 13, 0, 0, DateTimeKind.Utc).AddTicks(1_234_560));
        decoded.OccurredOn.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact(DisplayName = "O instante anterior à época Unix volta do Avro truncado em microssegundos")]
    public void RoundTrip_InstanteAnteriorAEpoca_VoltaTruncado()
    {
        DateTimeOffset instante = new DateTimeOffset(1969, 12, 31, 23, 59, 59, TimeSpan.Zero).AddTicks(9_999_995);

        ProcessoPublicadoAvro decoded = IdaEVolta(ProcessoPublicadoToAvroMapper.ToAvro(EventoEm(instante)));

        decoded.OccurredOn.Should().Be(new DateTime(1969, 12, 31, 23, 59, 59, DateTimeKind.Utc).AddTicks(9_999_990));
    }

    // O leitor converte o long pela unidade do schema de quem gravou, não pela do seu: a mensagem publicada antes da
    // troca para timestamp-micros continua lida no mesmo instante, na precisão de milissegundo que ela carregava.
    [Fact(DisplayName = "A mensagem gravada com o schema em milissegundos é lida pelo schema novo no mesmo instante")]
    public void SchemaAntigo_LidoPeloNovo_PreservaOInstante()
    {
        global::Avro.Schema schemaEmMilissegundos = global::Avro.Schema.Parse(
            ProcessoPublicadoAvro.AvroSchema.ToString().Replace("timestamp-micros", "timestamp-millis", StringComparison.Ordinal));
        DateTimeOffset instante = new DateTimeOffset(2026, 11, 13, 10, 0, 0, TimeSpan.Zero).AddTicks(1_234_567);

        ProcessoPublicadoAvro decoded = IdaEVolta(ProcessoPublicadoToAvroMapper.ToAvro(EventoEm(instante)), schemaEmMilissegundos);

        decoded.OccurredOn.Should().Be(new DateTime(2026, 11, 13, 10, 0, 0, DateTimeKind.Utc).AddTicks(1_230_000));
    }

    [Fact(DisplayName = "O schema declara o instante de emissão em timestamp-micros")]
    public void Schema_OccurredOn_DeclaraTimestampMicros()
    {
        global::Avro.RecordSchema schema = (global::Avro.RecordSchema)ProcessoPublicadoAvro.AvroSchema;

        global::Avro.LogicalSchema occurredOn = (global::Avro.LogicalSchema)schema.Fields.Single(f => f.Name == "OccurredOn").Schema;

        occurredOn.LogicalTypeName.Should().Be("timestamp-micros");
    }

    private static ProcessoPublicadoEvent EventoEm(DateTimeOffset instante) =>
        new(
            ProcessoSeletivoId: Guid.CreateVersion7(),
            EditalId: Guid.CreateVersion7(),
            SnapshotPublicacaoId: Guid.CreateVersion7(),
            HashConfiguracao: new string('c', 64),
            HashEdital: new string('d', 64),
            OccurredOn: instante);

    private static ProcessoPublicadoAvro IdaEVolta(ProcessoPublicadoAvro original) =>
        IdaEVolta(original, ProcessoPublicadoAvro.AvroSchema);

    private static ProcessoPublicadoAvro IdaEVolta(ProcessoPublicadoAvro original, global::Avro.Schema schemaDoProdutor)
    {
        SpecificDefaultWriter writer = new(schemaDoProdutor);
        using MemoryStream stream = new();
        BinaryEncoder encoder = new(stream);
        writer.Write(original, encoder);
        encoder.Flush();

        stream.Position = 0;
        SpecificDefaultReader reader = new(schemaDoProdutor, ProcessoPublicadoAvro.AvroSchema);
        return reader.Read(reuse: default(ProcessoPublicadoAvro), new BinaryDecoder(stream))!;
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
        ProcessoPublicadoAvro avro = ProcessoPublicadoToAvroMapper.ToAvro(
            EventoEm(new DateTimeOffset(2026, 5, 9, 12, 0, 0, TimeSpan.Zero)));

        object value = avro.Get(fieldPos);

        value.Should().NotBeNull();

        // Cobertura por nome para evitar drift de ordem entre .avsc e Get/Put.
        global::Avro.RecordSchema schema = (global::Avro.RecordSchema)ProcessoPublicadoAvro.AvroSchema;
        schema.Fields[fieldPos].Name.Should().Be(expectedFieldName);
    }

    [Fact(DisplayName = "ISpecificRecord Get com posição inválida lança AvroRuntimeException")]
    public void Get_PosicaoInvalida_DeveLancar()
    {
        ProcessoPublicadoAvro avro = new();
        Action act = () => avro.Get(99);
        act.Should().Throw<global::Avro.AvroRuntimeException>();
    }

    [Fact(DisplayName = "ISpecificRecord Put com posição inválida lança AvroRuntimeException")]
    public void Put_PosicaoInvalida_DeveLancar()
    {
        ProcessoPublicadoAvro avro = new();
        Action act = () => avro.Put(99, "x");
        act.Should().Throw<global::Avro.AvroRuntimeException>();
    }
}
