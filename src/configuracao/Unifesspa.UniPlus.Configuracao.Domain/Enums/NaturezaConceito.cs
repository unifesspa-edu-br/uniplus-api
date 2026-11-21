namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Natureza de um <see cref="Entities.TipoConceito"/>: define se o conceito é medido numa escala numérica
/// ou escolhido entre valores categóricos. A identidade gravada e trafegada é o código declarado (ADR-0176),
/// o mesmo do enum de mesmo nome da Seleção.
/// </summary>
[DescricaoDoContrato("Como o conceito que avalia uma etapa se expressa: numa escala numérica ou entre valores categóricos.")]
public enum NaturezaConceito
{
    /// <summary>Conceito medido numa escala de mínimo, máximo e casas decimais.</summary>
    [CodigoDeContrato("NUMERICO", "Numérico", Descricao = "Conceito medido numa escala de mínimo, máximo e casas decimais.")]
    Numerico = 1,

    /// <summary>Conceito escolhido entre valores categóricos ordenados.</summary>
    [CodigoDeContrato("CATEGORICO", "Categórico", Descricao = "Conceito escolhido entre valores categóricos ordenados.")]
    Categorico = 2,
}
