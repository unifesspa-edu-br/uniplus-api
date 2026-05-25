using Unifesspa.UniPlus.Spikes.EventSourcing.Application.Comandos;
using Unifesspa.UniPlus.Spikes.EventSourcing.Application.Portas;
using Unifesspa.UniPlus.Spikes.EventSourcing.Domain;
using Unifesspa.UniPlus.Spikes.EventSourcing.Domain.Eventos;
using Wolverine.Marten;

namespace Unifesspa.UniPlus.Spikes.EventSourcing.Infrastructure.Handlers;

/// <summary>
/// Retifica um edital publicado anexando um novo fato (<see cref="EditalRetificado"/>)
/// ao stream — correção nunca muta o passado.
/// </summary>
public static class RetificarEditalHandler
{
    public static async Task<Events> Handle(
        RetificarEdital comando,
        [WriteAggregate(nameof(RetificarEdital.EditalId))] EditalEs edital,
        IProtetorPii protetor,
        TimeProvider relogio,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(comando);
        ArgumentNullException.ThrowIfNull(edital);
        ArgumentNullException.ThrowIfNull(protetor);
        ArgumentNullException.ThrowIfNull(relogio);

        if (edital.Status != StatusEditalEs.Publicado)
        {
            throw new InvalidOperationException(
                $"Edital {comando.EditalId} não está publicado (status atual: {edital.Status}).");
        }

        AtorCifrado ator = await protetor.ProtegerAsync(comando.Ator, cancellationToken).ConfigureAwait(false);

        return [new EditalRetificado(comando.EditalId, comando.Motivo, ator, relogio.GetUtcNow())];
    }
}
