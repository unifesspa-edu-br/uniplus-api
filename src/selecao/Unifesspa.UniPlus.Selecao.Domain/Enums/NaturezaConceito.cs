namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Como o conceito que avalia uma etapa se expressa: medido numa escala numérica ou escolhido entre valores
/// categóricos. Sem membro sentinela: o conceito que a etapa declara tem sempre uma natureza.
/// </summary>
public enum NaturezaConceito
{
    /// <summary>Conceito medido numa escala de mínimo, máximo e casas decimais.</summary>
    Numerico = 1,

    /// <summary>Conceito escolhido entre valores categóricos ordenados, cada um contando ou não como aprovação.</summary>
    Categorico = 2,
}
