namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Códigos canônicos das regras de <c>tipo=criterio_desempate</c> do
/// <c>regras_do_catalogo</c> (Story #772) reconhecidas pela configuração de
/// desempate (Story #774).
/// </summary>
public static class CriterioDesempateCodigo
{
    /// <summary>Ordena o subgrupo pela nota de uma etapa do processo (parâmetros: <c>etapa_ref</c>).</summary>
    public const string MaiorNotaEtapa = "DESEMPATE-MAIOR-NOTA-ETAPA";

    /// <summary>Ordena por data de nascimento — nascido mais cedo vence (sem parâmetros).</summary>
    public const string MaiorIdade = "DESEMPATE-MAIOR-IDADE";

    /// <summary>Prioriza quem satisfaz <c>FAIXA_ETARIA ≥ idade_minima</c> (Lei 10.741/2003 art. 27).</summary>
    public const string Idoso = "DESEMPATE-IDOSO";

    /// <summary>Prioriza quem satisfaz um predicado tipado sobre um fato do candidato (parâmetros: <c>fato</c>/<c>operador</c>/<c>valor</c>).</summary>
    public const string PredicadoFato = "DESEMPATE-PREDICADO-FATO";

    /// <summary>
    /// Ordena pela maior nota numa área do ENEM, percorrendo as áreas na ordem declarada
    /// (parâmetros: <c>areas</c>, os códigos das áreas do quadro de pesos por área congelado na
    /// classificação).
    /// </summary>
    public const string MaiorNotaAreaEnem = "DESEMPATE-MAIOR-NOTA-AREA-ENEM";
}
