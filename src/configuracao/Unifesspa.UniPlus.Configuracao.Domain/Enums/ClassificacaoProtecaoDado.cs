namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Classificação de proteção de dados de um <see cref="Entities.FatoCandidato"/>, na escala da
/// ADR-0081 (ADR-0136). <see cref="Sensivel"/> diz a natureza do dado (LGPD, art. 5º, II); os
/// demais níveis dizem a exposição permitida a um dado que é sempre pessoal quando ligado a um
/// candidato identificado.
/// </summary>
public enum ClassificacaoProtecaoDado
{
    /// <summary>Sentinela — classificação não informada; rejeitada na criação.</summary>
    Nenhuma = 0,

    /// <summary>Exposição sem restrição de divulgação.</summary>
    Publico,

    /// <summary>Exposição restrita ao âmbito institucional.</summary>
    Interno,

    /// <summary>Dado pessoal (LGPD, art. 5º, I).</summary>
    Pessoal,

    /// <summary>
    /// Dado pessoal que identifica diretamente o titular: número de documento (CPF, RG,
    /// passaporte). Isola um indivíduo entre os demais sem revelar nenhum atributo seu —
    /// por isso cifrado em repouso, diferente de <see cref="Pessoal"/> e <see cref="Sensivel"/>
    /// (ADR-0121, ADR-0136, emenda de #1856).
    /// </summary>
    Identificador,

    /// <summary>Dado pessoal sensível (LGPD, art. 5º, II), como origem racial ou saúde.</summary>
    Sensivel,
}
