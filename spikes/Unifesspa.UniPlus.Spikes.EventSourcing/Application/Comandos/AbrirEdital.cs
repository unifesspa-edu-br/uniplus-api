namespace Unifesspa.UniPlus.Spikes.EventSourcing.Application.Comandos;

/// <summary>Comando: abrir um novo edital (inicia o stream do agregado).</summary>
public sealed record AbrirEdital(
    Guid EditalId,
    string NumeroEdital,
    string Titulo,
    Ator Ator);
