namespace Unifesspa.UniPlus.Publicacoes.Application.Commands.AtosNormativos;

using Unifesspa.UniPlus.Publicacoes.Application.DTOs;

/// <summary>
/// Resultado do registro de um ato: o identificador atribuído, o instante
/// forense em que o registro entrou no sistema (<see cref="RegistradoEm"/> — o
/// que a ADR-0106 exige propagar de volta a quem orquestra) e os avisos de
/// numeração detectados no momento do registro (AC4).
/// </summary>
public sealed record RegistrarAtoNormativoResult(
    Guid AtoId,
    DateTimeOffset RegistradoEm,
    IReadOnlyList<AvisoNumeracao> Avisos);
