namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Hipótese legal de tratamento declarada por um <see cref="Entities.FatoCandidato"/> (ADR-0136):
/// as do art. 7º da LGPD para o dado pessoal e as do art. 11 para o dado sensível. A
/// admissibilidade por classificação vive em <see cref="HipotesesLegaisTratamento.AdmiteClassificacao"/>.
/// </summary>
public enum HipoteseLegalTratamento
{
    /// <summary>Sentinela — hipótese não informada; rejeitada na criação.</summary>
    Nenhuma = 0,

    /// <summary>Consentimento do titular (art. 7º, I; art. 11, I).</summary>
    Consentimento,

    /// <summary>Cumprimento de obrigação legal ou regulatória (art. 7º, II; art. 11, II, a).</summary>
    CumprimentoObrigacaoLegal,

    /// <summary>Execução de políticas públicas pela administração pública (art. 7º, III; art. 11, II, b).</summary>
    ExecucaoPoliticasPublicas,

    /// <summary>Estudos por órgão de pesquisa (art. 7º, IV; art. 11, II, c).</summary>
    EstudosPorOrgaoDePesquisa,

    /// <summary>Execução de contrato de que o titular é parte (art. 7º, V).</summary>
    ExecucaoContrato,

    /// <summary>Exercício regular de direitos em processo (art. 7º, VI; art. 11, II, d).</summary>
    ExercicioRegularDeDireitos,

    /// <summary>Proteção da vida ou da incolumidade física (art. 7º, VII; art. 11, II, e).</summary>
    ProtecaoDaVida,

    /// <summary>Tutela da saúde (art. 7º, VIII; art. 11, II, f).</summary>
    TutelaDaSaude,

    /// <summary>Interesse legítimo do controlador ou de terceiro (art. 7º, IX).</summary>
    InteresseLegitimo,

    /// <summary>Proteção do crédito (art. 7º, X).</summary>
    ProtecaoDoCredito,

    /// <summary>Prevenção à fraude e segurança do titular (art. 11, II, g).</summary>
    PrevencaoAFraude,
}
