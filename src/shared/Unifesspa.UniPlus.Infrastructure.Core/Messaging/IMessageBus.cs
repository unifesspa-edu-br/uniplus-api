namespace Unifesspa.UniPlus.Infrastructure.Core.Messaging;

public interface IMessageBus
{
    Task PublicarAsync<T>(string topico, T mensagem, CancellationToken cancellationToken = default) where T : class;
    Task PublicarAsync<T>(string topico, string chave, T mensagem, CancellationToken cancellationToken = default) where T : class;
}
