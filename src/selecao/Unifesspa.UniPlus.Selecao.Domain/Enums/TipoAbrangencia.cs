namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Abrangência de uma <see cref="Entities.DocumentoExigidoBaseLegal"/> (Story #554, PR #898,
/// issue #549, ADR-0074). <c>InternaEdital</c> conta sozinha como base legal — as demais
/// referenciam normas externas ao próprio edital.
/// </summary>
public enum TipoAbrangencia
{
    Nenhuma = 0,
    Federal = 1,
    Estadual = 2,
    Municipal = 3,
    InternaNorma = 4,
    InternaEdital = 5,
}
