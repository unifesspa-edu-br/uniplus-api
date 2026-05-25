namespace Unifesspa.UniPlus.Spikes.EventSourcing.Application.Comandos;

/// <summary>
/// Comando de <b>injeção de falha</b> (prova de rollback, gate G1): o handler anexa
/// um evento e publica uma mensagem de integração e então lança — provando que nem
/// o evento nem o envelope do outbox são commitados.
/// </summary>
public sealed record FalharAposAnexar(
    Guid EditalId,
    string Motivo,
    Ator Ator);
