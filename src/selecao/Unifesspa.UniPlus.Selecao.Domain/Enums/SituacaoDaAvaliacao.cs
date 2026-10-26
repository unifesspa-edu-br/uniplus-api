namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Como a nota da etapa de uma oferta concorrida foi (ou não) apurada.</summary>
public enum SituacaoDaAvaliacao
{
    /// <summary>A nota foi calculada pelos pesos do grupo do curso.</summary>
    Apurada = 1,

    /// <summary>Sem nota: a inscrição não aparece no arquivo do lote.</summary>
    InscritoSemLinha = 2,

    /// <summary>Sem nota: a inscrição tem mais de uma linha diferente no arquivo do lote.</summary>
    EmConflito = 3,
}
