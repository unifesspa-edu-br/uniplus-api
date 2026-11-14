namespace Unifesspa.UniPlus.Publicacoes.Application.Commands.AtosNormativos;

using Unifesspa.UniPlus.Publicacoes.Application.DTOs;

/// <summary>
/// Resultado do registro de um ato: o identificador atribuído, o instante
/// da divulgação gravado (<see cref="PublicadoEm"/>) e os avisos de
/// numeração detectados no momento do registro (AC4).
/// </summary>
public sealed record RegistrarAtoNormativoResult(
    Guid AtoId,
    DateTimeOffset PublicadoEm,
    IReadOnlyList<AvisoNumeracao> Avisos);
