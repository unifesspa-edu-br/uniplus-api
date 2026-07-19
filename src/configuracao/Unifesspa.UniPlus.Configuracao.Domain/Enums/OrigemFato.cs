namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Origem do dado de um <see cref="Entities.FatoCandidato"/> — como o valor do fato
/// chega ao sistema (ADR-0111, refinada pela ADR-0116). É ortogonal ao domínio
/// (tipo de dado) e à cardinalidade; serve à invariante, feita cumprir pelo
/// avaliador, de que um fato <see cref="Derivado"/> não pode ser insumo de si
/// mesmo nem de um gatilho avaliado antes da etapa que o produz.
/// </summary>
public enum OrigemFato
{
    /// <summary>Sentinela — origem não informada; rejeitada na criação.</summary>
    Nenhuma = 0,

    /// <summary>Computado pelo motor — não é coletado, é saída (ex.: faixa etária a partir da data de nascimento).</summary>
    Derivado,

    /// <summary>Resposta/seleção direta do candidato no cadastro de inscrição (ex.: COR_RACA, SEXO).</summary>
    Declarado,

    /// <summary>Recebido de uma integração externa (ex.: SIGAA, #874) — reservado, sem fato semeado ainda.</summary>
    Integracao,
}
