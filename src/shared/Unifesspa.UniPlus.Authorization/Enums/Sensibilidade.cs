namespace Unifesspa.UniPlus.Authorization.Enums;

/// <summary>
/// Classificação de sensibilidade do dado retornado por uma operação, usada
/// pela decisão de autorização para aplicar proteção por permissão e base legal
/// (LGPD-by-design, ADR-0078). A escala é crescente: <see cref="Publica"/> é o
/// dado de menor proteção e <see cref="Sensivel"/> o de maior.
/// </summary>
public enum Sensibilidade
{
    /// <summary>Dado público — sem restrição de divulgação.</summary>
    Publica = 0,

    /// <summary>Dado interno — restrito ao âmbito institucional.</summary>
    Interna = 1,

    /// <summary>Dado pessoal — protegido pela LGPD (art. 5º, I).</summary>
    Pessoal = 2,

    /// <summary>Dado pessoal sensível — protegido pela LGPD (art. 5º, II).</summary>
    Sensivel = 3,
}
