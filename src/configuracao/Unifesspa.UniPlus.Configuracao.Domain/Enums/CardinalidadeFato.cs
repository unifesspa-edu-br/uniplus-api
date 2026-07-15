namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Cardinalidade do valor de um <see cref="Entities.FatoCandidato"/> sobre um
/// candidato (ADR-0111) — se o fato assume um único valor ou um conjunto. Torna
/// a semântica dos operadores decidível sem que o avaliador (#847) precise
/// conhecer o catálogo por dentro: para um fato <see cref="Multivalorado"/>,
/// <c>IGUAL X</c> significa "X pertence ao conjunto do candidato" e <c>EM [..]</c>
/// significa "a interseção não é vazia"; para <see cref="Escalar"/>, comparação
/// direta.
/// </summary>
public enum CardinalidadeFato
{
    /// <summary>Sentinela — cardinalidade não informada; rejeitada na criação.</summary>
    Nenhuma = 0,

    /// <summary>O candidato tem um único valor (ex.: COR_RACA, SEXO, RENDA_PER_CAPITA).</summary>
    Escalar,

    /// <summary>O candidato pode ter mais de um valor (ex.: MODALIDADE, CONDICAO_ATENDIMENTO).</summary>
    Multivalorado,
}
