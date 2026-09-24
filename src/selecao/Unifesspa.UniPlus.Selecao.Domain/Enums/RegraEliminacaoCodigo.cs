namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Códigos canônicos das regras de <c>tipo=regra_eliminacao</c> do
/// <c>rol_de_regras</c> (Story #772) — eliminação por cálculo, cardinalidade
/// múltipla.
/// </summary>
public static class RegraEliminacaoCodigo
{
    /// <summary>Nota abaixo do mínimo na etapa referenciada elimina o candidato (args: <c>etapa_ref</c>, <c>nota_minima</c>).</summary>
    public const string ElimNotaMinimaEtapa = "ELIM-NOTA-MINIMA-ETAPA";

    /// <summary>Nota de uma área do ENEM abaixo do mínimo elimina (args: <c>area_codigo</c>, <c>minimo</c>; ex.: Redação 400 — Res. 805 Anexo I). No máximo uma por área. Só em processo baseado em ENEM.</summary>
    public const string ElimCorteEmArea = "ELIM-CORTE-EM-AREA";

    /// <summary>Nota zero em qualquer área do ENEM elimina (Res. 805 art. 5º; sem args). Só em processo baseado em ENEM.</summary>
    public const string ElimZeroEmArea = "ELIM-ZERO-EM-AREA";

    /// <summary>Falta em pelo menos um dia de prova da edição do ENEM usada no processo elimina; sem participação registrada na edição indicada equivale a falta (sem args). Só em processo baseado em ENEM.</summary>
    public const string ElimFaltaEmDiaDeProvaEnem = "ELIM-FALTA-EM-DIA-DE-PROVA-ENEM";
}
