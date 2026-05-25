namespace Unifesspa.UniPlus.Spikes.EventSourcing.Domain.Eventos;

/// <summary>
/// Fato de negócio: o edital foi publicado. Carrega o hash da configuração
/// congelada aplicada na decisão (amarra RN08 / ADR-0013 — "com qual config este
/// edital foi publicado?" é respondível pelo próprio histórico).
/// </summary>
public sealed record EditalPublicado(
    Guid EditalId,
    string HashConfiguracao,
    AtorCifrado Ator,
    DateTimeOffset OcorridoEm);
