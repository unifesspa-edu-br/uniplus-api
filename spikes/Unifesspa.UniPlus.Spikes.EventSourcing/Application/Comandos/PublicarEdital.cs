namespace Unifesspa.UniPlus.Spikes.EventSourcing.Application.Comandos;

/// <summary>Comando: publicar um edital em rascunho.</summary>
public sealed record PublicarEdital(
    Guid EditalId,
    string HashConfiguracao,
    Ator Ator);
