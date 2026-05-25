using Unifesspa.UniPlus.Spikes.EventSourcing.Application.EventosIntegracao;

namespace Unifesspa.UniPlus.Spikes.EventSourcing.Infrastructure.Handlers;

/// <summary>
/// Consome o evento de integração entregue pelo outbox e o registra no
/// <see cref="ColetorIntegracao"/> — evidência de que a mensagem cascateada foi
/// efetivamente entregue após o commit do append.
/// </summary>
public static class EditalPublicadoIntegradoHandler
{
    public static void Handle(EditalPublicadoIntegrado evento, ColetorIntegracao coletor)
    {
        ArgumentNullException.ThrowIfNull(evento);
        ArgumentNullException.ThrowIfNull(coletor);

        coletor.Registrar(evento);
    }
}
