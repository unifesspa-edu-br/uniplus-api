namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Authorization;

using Confluent.Kafka;

using unifesspa.uniplus.autorizacao.registrodeacesso;

/// <summary>
/// Serializador do lote para os testes do produtor: devolve bytes fixos, ou o que o cenário mandar fazer (falhar, ou
/// nunca terminar, como o Schema Registry que não responde).
/// </summary>
internal sealed class SerializadorDoLoteDeTeste(Func<Task<byte[]>>? comportamento = null) : IAsyncSerializer<LoteDoRegistroDeAcesso>
{
    public static readonly byte[] BytesDoLote = [0, 1, 2];

    public Task<byte[]> SerializeAsync(LoteDoRegistroDeAcesso data, SerializationContext context) =>
        comportamento is null ? Task.FromResult(BytesDoLote) : comportamento();
}
