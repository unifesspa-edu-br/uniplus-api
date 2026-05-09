// O namespace abaixo não é PascalCase porque deve casar exatamente com o
// `namespace` declarado no schema Avro (Apache.Avro NET resolve a classe via
// reflection usando <namespace>.<name> qualificado). Manter alinhado com
// `Events/Schemas/EditalPublicado.avsc` em Selecao.Domain.
#pragma warning disable CA1050 // Declare types in namespaces — namespace declarado, mas em lowercase.
namespace unifesspa.uniplus.selecao.events;
#pragma warning restore CA1050

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using global::Avro;
using global::Avro.Specific;
using EditalPublicadoEvent = Unifesspa.UniPlus.Selecao.Domain.Events.EditalPublicadoEvent;

/// <summary>
/// Wire-form Avro do <see cref="EditalPublicadoEvent"/> para publicação em Kafka via
/// Confluent Schema Registry (Apicurio em modo Confluent-compat).
/// </summary>
/// <remarks>
/// <para>
/// Classe escrita à mão (não codegen) seguindo a interface <c>ISpecificRecord</c> do
/// Apache Avro 1.12. <b>Namespace e nome devem casar exatamente</b> com o declarado em
/// <c>Events/Schemas/EditalPublicado.avsc</c> (Selecao.Domain) — Apache.Avro NET usa
/// reflection (<c>Type.GetType("&lt;namespace&gt;.&lt;name&gt;")</c>) na desserialização
/// para instanciar o tipo. Drift = <c>Avro.AvroException: Unable to find type</c>
/// no consumer.
/// </para>
/// <para>
/// O schema é carregado uma única vez do embedded resource em
/// <see cref="EditalPublicadoEvent"/>'s assembly (Selecao.Domain) — single source of
/// truth, evita drift entre o JSON publicado no Apicurio e a forma serializada.
/// </para>
/// <para>
/// Mapeamento <see cref="EditalPublicadoEvent"/> → <see cref="EditalPublicado"/> fica em
/// <c>Selecao.Infrastructure.Messaging.EditalPublicadoToAvroMapper</c> e é executado
/// pelo cascading handler <c>EditalPublicadoToKafkaCascadeHandler</c> ao consumir o
/// evento da PG queue intra-módulo. Consumidores cross-módulo recuperam o schema do
/// Apicurio via schema-id no envelope da mensagem — não dependem deste assembly.
/// </para>
/// </remarks>
[SuppressMessage(
    "Naming",
    "CA1707:Identifiers should not contain underscores",
    Justification = "Namespace lowercase exigido pelo binding Apache.Avro NET (precisa casar com 'namespace' do .avsc).")]
[SuppressMessage(
    "Naming",
    "CA1300:Specify MessageBoxOptions",
    Justification = "False positive — atributo aplicável a UI; classe é POCO de mensagem.")]
public sealed class EditalPublicado : ISpecificRecord
{
    public const string SchemaResourceName = "Unifesspa.UniPlus.Selecao.Domain.Events.Schemas.EditalPublicado.avsc";

    private static readonly Lazy<Schema> SchemaInstance = new(LoadSchemaFromDomainAssembly);

    /// <summary>Schema Avro carregado do embedded resource em Selecao.Domain.</summary>
    public static Schema AvroSchema => SchemaInstance.Value;

    /// <inheritdoc />
    public Schema Schema => AvroSchema;

    /// <summary>UUID v7 do evento (rastreabilidade fim-a-fim, idempotência cross-consumer).</summary>
    public string EventId { get; set; } = string.Empty;

    /// <summary>
    /// Instante de emissão UTC. O schema declara <c>{type:long, logicalType:timestamp-millis}</c>;
    /// Apache.Avro converte automaticamente entre <see cref="DateTime"/> (logical) e
    /// <see cref="long"/> (base, ms desde epoch Unix) na serialização.
    /// </summary>
    public DateTime OccurredOn { get; set; }

    /// <summary>UUID v7 do agregado Edital (chave estável cross-módulo).</summary>
    public string EditalId { get; set; } = string.Empty;

    /// <summary>Identificador humano do edital, e.g. "001/2026".</summary>
    public string NumeroEdital { get; set; } = string.Empty;

    /// <inheritdoc />
    public object Get(int fieldPos) => fieldPos switch
    {
        0 => EventId,
        1 => OccurredOn,
        2 => EditalId,
        3 => NumeroEdital,
        _ => throw new AvroRuntimeException($"Posição de campo inválida em EditalPublicado.Get: {fieldPos}"),
    };

    /// <inheritdoc />
    public void Put(int fieldPos, object fieldValue)
    {
        switch (fieldPos)
        {
            case 0:
                EventId = (string)fieldValue;
                break;
            case 1:
                OccurredOn = (DateTime)fieldValue;
                break;
            case 2:
                EditalId = (string)fieldValue;
                break;
            case 3:
                NumeroEdital = (string)fieldValue;
                break;
            default:
                throw new AvroRuntimeException($"Posição de campo inválida em EditalPublicado.Put: {fieldPos}");
        }
    }

    private static Schema LoadSchemaFromDomainAssembly()
    {
        System.Reflection.Assembly asm = typeof(EditalPublicadoEvent).Assembly;
        using Stream? stream = asm.GetManifestResourceStream(SchemaResourceName)
            ?? throw new InvalidOperationException(
                $"Schema Avro '{SchemaResourceName}' não encontrado como embedded resource em '{asm.FullName}'. " +
                "Verifique que o arquivo está em Selecao.Domain/Events/Schemas/ e que o csproj inclui <EmbeddedResource Include=\"Events\\Schemas\\*.avsc\" />.");
        using StreamReader reader = new(stream);
        return Schema.Parse(reader.ReadToEnd());
    }
}
