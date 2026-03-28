namespace Unifesspa.UniPlus.Infrastructure.Common.Messaging;

using System.Text.Json;

using Confluent.Kafka;

public sealed class KafkaProducer : IMessageBus, IDisposable
{
    private readonly IProducer<string, string> _producer;

    public KafkaProducer(IProducer<string, string> producer)
    {
        _producer = producer;
    }

    public async Task PublicarAsync<T>(string topico, T mensagem, CancellationToken cancellationToken = default) where T : class
    {
        await PublicarAsync(topico, Guid.NewGuid().ToString(), mensagem, cancellationToken).ConfigureAwait(false);
    }

    public async Task PublicarAsync<T>(string topico, string chave, T mensagem, CancellationToken cancellationToken = default) where T : class
    {
        string json = JsonSerializer.Serialize(mensagem);
        Message<string, string> message = new() { Key = chave, Value = json };
        await _producer.ProduceAsync(topico, message, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose() => _producer.Dispose();
}
