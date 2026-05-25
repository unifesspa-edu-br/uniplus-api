namespace Unifesspa.UniPlus.Spikes.EventSourcing.Coexistencia.Handlers;

/// <summary>Registra a entrega do evento de integração do módulo CRUD (outbox 'main').</summary>
public static class RegistroCrudCriadoHandler
{
    public static void Handle(RegistroCrudCriado evento, ColetorCoexistencia coletor)
    {
        ArgumentNullException.ThrowIfNull(evento);
        ArgumentNullException.ThrowIfNull(coletor);
        coletor.Registrar(evento.Id);
    }
}
