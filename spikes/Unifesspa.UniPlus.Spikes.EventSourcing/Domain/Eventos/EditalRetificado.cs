namespace Unifesspa.UniPlus.Spikes.EventSourcing.Domain.Eventos;

/// <summary>
/// Fato de negócio: o edital publicado foi retificado. Correção é um <b>novo
/// fato</b> no presente, nunca mutação destrutiva do passado (append-only).
/// </summary>
public sealed record EditalRetificado(
    Guid EditalId,
    string Motivo,
    AtorCifrado Ator,
    DateTimeOffset OcorridoEm);
