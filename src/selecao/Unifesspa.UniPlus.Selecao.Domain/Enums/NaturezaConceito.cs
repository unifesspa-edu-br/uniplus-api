namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Como o conceito que avalia uma etapa se expressa: medido numa escala numérica ou escolhido entre valores
/// categóricos. Sem membro sentinela: o conceito que a etapa declara tem sempre uma natureza. O código é o
/// mesmo do enum de mesmo nome da Configuração (ADR-0176).
/// </summary>
[DescricaoDoContrato("Como o conceito que avalia uma etapa se expressa: numa escala numérica ou entre valores categóricos.")]
public enum NaturezaConceito
{
    /// <summary>Conceito medido numa escala de mínimo, máximo e casas decimais.</summary>
    [CodigoDeContrato("NUMERICO", "Numérico", Descricao = "Conceito medido numa escala de mínimo, máximo e casas decimais.")]
    Numerico = 1,

    /// <summary>Conceito escolhido entre valores categóricos ordenados, cada um contando ou não como aprovação.</summary>
    [CodigoDeContrato("CATEGORICO", "Categórico", Descricao = "Conceito escolhido entre valores categóricos ordenados.")]
    Categorico = 2,
}
