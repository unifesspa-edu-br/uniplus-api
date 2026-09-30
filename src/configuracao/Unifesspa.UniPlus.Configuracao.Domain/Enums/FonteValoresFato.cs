namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// De onde vêm os valores de um <see cref="Entities.FatoCandidato"/> categórico (ADR-0136).
/// Substitui a distinção pela nulidade dos valores de domínio: quem consome o fato decide pela
/// fonte, e nunca pelo código do fato.
/// </summary>
public enum FonteValoresFato
{
    /// <summary>Sentinela — fonte não informada; rejeitada na criação de fato categórico.</summary>
    Nenhuma = 0,

    /// <summary>Os valores vivem no próprio catálogo (ex.: COR_RACA, SEXO, NACIONALIDADE).</summary>
    Global,

    /// <summary>
    /// Os valores são as opções que cada processo declara, congeladas no edital (ex.:
    /// CONDICAO_ATENDIMENTO e TIPO_DEFICIENCIA, a partir da oferta de atendimento).
    /// </summary>
    Processo,

    /// <summary>Os valores são as modalidades ofertadas pelo processo (MODALIDADE).</summary>
    Modalidade,
}
