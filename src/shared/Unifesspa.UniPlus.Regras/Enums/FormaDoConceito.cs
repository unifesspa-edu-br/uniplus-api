namespace Unifesspa.UniPlus.Regras.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Como o conceito que avalia uma etapa se expressa: medido numa escala numérica ou escolhido entre valores de uma
/// lista de opções. Sem membro sentinela: o conceito que a etapa declara tem sempre uma forma. É o mesmo enum na
/// Configuração, que cadastra o tipo de conceito, e na Seleção, que o congela na etapa do processo (ADR-0176).
/// </summary>
[DescricaoDoContrato("Como o conceito que avalia uma etapa se expressa: numa escala numérica ou entre opções de uma lista.")]
public enum FormaDoConceito
{
    /// <summary>Conceito medido numa escala de mínimo, máximo e casas decimais.</summary>
    [CodigoDeContrato("NUMERICO", "Numérico", Descricao = "Conceito medido numa escala de mínimo, máximo e casas decimais.")]
    Numerico = 1,

    /// <summary>Conceito escolhido entre as opções ordenadas de uma lista, cada uma contando ou não como aprovação.</summary>
    [CodigoDeContrato("POR_OPCOES", "Por opções", Descricao = "Conceito escolhido entre as opções ordenadas de uma lista, cada uma contando ou não como aprovação.")]
    PorOpcoes = 2,
}
