namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Origem do dado de um <see cref="Entities.FatoCandidato"/> — como o fato chega
/// ao sistema (ADR-0111). É ortogonal ao domínio (tipo de dado) e à cardinalidade;
/// serve à invariante, feita cumprir pelo avaliador, de que um fato
/// <see cref="Derivado"/> não pode ser insumo de si mesmo nem de um gatilho
/// avaliado antes da etapa que o produz.
/// </summary>
public enum NaturezaFato
{
    /// <summary>Sentinela — natureza não informada; rejeitada na criação.</summary>
    Nenhuma = 0,

    /// <summary>Respondido pelo candidato (PCD, COR_RACA, RENDA_PER_CAPITA).</summary>
    BrutoInformado,

    /// <summary>Declaração de desejo, não de elegibilidade (ex.: optar por concorrer numa cota).</summary>
    DeVontade,

    /// <summary>Computado pelo motor — não é coletado, é saída (ex.: modalidade derivada, cotista).</summary>
    Derivado,
}
