namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Códigos canônicos das regras de <c>tipo=criterio_desempate</c> do
/// <c>rol_de_regras</c> (Story #772) reconhecidas pela configuração de
/// desempate (Story #774, modelagem P-B §2.6).
/// </summary>
public static class CriterioDesempateCodigo
{
    /// <summary>Ordena o subgrupo pela nota de uma etapa do processo (args: <c>etapa_ref</c>).</summary>
    public const string MaiorNotaEtapa = "DESEMPATE-MAIOR-NOTA-ETAPA";

    /// <summary>Ordena por data de nascimento — nascido mais cedo vence (sem args).</summary>
    public const string MaiorIdade = "DESEMPATE-MAIOR-IDADE";

    /// <summary>Prioriza quem satisfaz <c>FAIXA_ETARIA ≥ idade_minima</c> (Lei 10.741/2003 art. 27).</summary>
    public const string Idoso = "DESEMPATE-IDOSO";

    /// <summary>Prioriza quem satisfaz um predicado tipado sobre um fato do candidato (args: <c>fato</c>/<c>operador</c>/<c>valor</c>).</summary>
    public const string PredicadoFato = "DESEMPATE-PREDICADO-FATO";
}
