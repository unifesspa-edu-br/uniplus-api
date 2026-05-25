namespace Unifesspa.UniPlus.Spikes.EventSourcing.Application.Comandos;

/// <summary>Comando: retificar um edital publicado (correção como novo fato).</summary>
public sealed record RetificarEdital(
    Guid EditalId,
    string Motivo,
    Ator Ator);
