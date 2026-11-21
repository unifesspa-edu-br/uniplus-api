namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Formato pedagógico de uma <see cref="Entities.OfertaCurso"/> (ADR-0066). Quando o pedido não informa o formato, vale
/// <see cref="Presencial"/>. A ordem de declaração é a de exibição.
/// </summary>
public enum FormatoPedagogico
{
    /// <summary>Oferta presencial, o formato quando o pedido não informa.</summary>
    [CodigoDeContrato("PRESENCIAL", "Presencial")]
    Presencial = 1,

    /// <summary>Oferta semipresencial, que combina aulas presenciais e a distância.</summary>
    [CodigoDeContrato("SEMIPRESENCIAL", "Semipresencial")]
    Semipresencial = 2,

    /// <summary>Oferta a distância.</summary>
    [CodigoDeContrato("EAD", "A distância")]
    Ead = 3,
}
