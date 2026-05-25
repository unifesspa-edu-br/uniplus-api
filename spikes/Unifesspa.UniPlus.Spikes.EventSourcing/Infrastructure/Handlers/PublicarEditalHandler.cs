using Unifesspa.UniPlus.Spikes.EventSourcing.Application.Comandos;
using Unifesspa.UniPlus.Spikes.EventSourcing.Application.EventosIntegracao;
using Unifesspa.UniPlus.Spikes.EventSourcing.Application.Portas;
using Unifesspa.UniPlus.Spikes.EventSourcing.Domain;
using Unifesspa.UniPlus.Spikes.EventSourcing.Domain.Eventos;
using Wolverine;
using Wolverine.Marten;

namespace Unifesspa.UniPlus.Spikes.EventSourcing.Infrastructure.Handlers;

/// <summary>
/// Publica um edital pelo Aggregate Handler Workflow do Wolverine: a middleware faz
/// <c>FetchForWriting&lt;EditalEs&gt;</c>, invoca este método e anexa os eventos
/// retornados, tudo num único <c>SaveChangesAsync</c>. Os <see cref="Events"/> vão
/// para o stream; as <see cref="OutgoingMessages"/> são instaladas no outbox na
/// mesma transação do append (atomicidade write+evento, gate G1).
/// </summary>
public static class PublicarEditalHandler
{
    public static async Task<(Events, OutgoingMessages)> Handle(
        PublicarEdital comando,
        [WriteAggregate(nameof(PublicarEdital.EditalId))] EditalEs edital,
        IProtetorPii protetor,
        TimeProvider relogio,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(comando);
        ArgumentNullException.ThrowIfNull(edital);
        ArgumentNullException.ThrowIfNull(protetor);
        ArgumentNullException.ThrowIfNull(relogio);

        if (edital.Status != StatusEditalEs.Rascunho)
        {
            throw new InvalidOperationException(
                $"Edital {comando.EditalId} não está em rascunho (status atual: {edital.Status}).");
        }

        AtorCifrado ator = await protetor.ProtegerAsync(comando.Ator, cancellationToken).ConfigureAwait(false);
        DateTimeOffset agora = relogio.GetUtcNow();

        Events eventos = [new EditalPublicado(comando.EditalId, comando.HashConfiguracao, ator, agora)];
        OutgoingMessages mensagens = [new EditalPublicadoIntegrado(comando.EditalId, edital.NumeroEdital, agora)];

        return (eventos, mensagens);
    }
}
