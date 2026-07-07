namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Códigos canônicos das regras de <c>tipo=regra_eliminacao</c> do
/// <c>rol_de_regras</c> (Story #772) — eliminação por cálculo, cardinalidade
/// múltipla (modelagem P-B §2.5).
/// </summary>
public static class RegraEliminacaoCodigo
{
    /// <summary>Nota abaixo do mínimo na etapa referenciada elimina o candidato (args: <c>etapa_ref</c>, <c>nota_minima</c>).</summary>
    public const string ElimNotaMinimaEtapa = "ELIM-NOTA-MINIMA-ETAPA";

    /// <summary>Nota de redação (ENEM) abaixo do mínimo elimina (args: <c>minimo</c>, ex. 400 — Res. 805 Anexo I). Só em processo baseado em ENEM.</summary>
    public const string ElimCorteRedacao = "ELIM-CORTE-REDACAO";

    /// <summary>Nota zero em qualquer área do ENEM elimina (Res. 805 art. 5º; sem args). Só em processo baseado em ENEM.</summary>
    public const string ElimZeroEmArea = "ELIM-ZERO-EM-AREA";
}
