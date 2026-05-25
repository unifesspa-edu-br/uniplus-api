namespace Unifesspa.UniPlus.Spikes.EventSourcing.Domain.Eventos;

/// <summary>
/// Fato de negócio: um edital foi aberto (início do stream). Nome de fato de
/// domínio no passado, nunca <c>EditalCriadoUpdated</c> e afins (governança ADR-0069).
/// </summary>
public sealed record EditalAberto(
    Guid EditalId,
    string NumeroEdital,
    string Titulo,
    AtorCifrado Ator,
    DateTimeOffset OcorridoEm);
