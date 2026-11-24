namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Como a nota da etapa de uma oferta concorrida foi (ou não) apurada.</summary>
public enum SituacaoDaAvaliacao
{
    /// <summary>A nota foi calculada pelos pesos do grupo do curso.</summary>
    [CodigoDeContrato("APURADA", "Apurada", Descricao = "A nota foi calculada pelos pesos do grupo do curso.")]
    Apurada = 1,

    /// <summary>Sem nota: a inscrição não aparece no arquivo do lote.</summary>
    [CodigoDeContrato("INSCRITO_SEM_LINHA", "Inscrito sem linha", Descricao = "A inscrição não aparece no arquivo.")]
    InscritoSemLinha = 2,

    /// <summary>Sem nota: a inscrição tem mais de uma linha diferente no arquivo do lote.</summary>
    [CodigoDeContrato("EM_CONFLITO", "Em conflito", Descricao = "A inscrição tem linhas diferentes no arquivo.")]
    EmConflito = 3,
}
