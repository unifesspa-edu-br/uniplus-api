namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Enums;

/// <summary>
/// Como a nota da etapa é declarada e obtida: o peso, a nota mínima, a escala em que a nota é lançada
/// (máxima e passo), a forma de avaliação, a regra que consolida as notas dos avaliadores e a escala de
/// atendimento. É entrada de fábrica de vida curta, superficialmente imutável: carrega a coleção
/// somente leitura e as instâncias de <see cref="ReferenciaRegra"/> e <see cref="NivelDeAtendimento"/>
/// que a etapa passa a possuir, e nunca é guardada pela entidade, que copia os valores.
/// </summary>
/// <remarks>
/// A escala de atendimento admite elemento nulo porque a conferência da forma, que roda antes de
/// qualquer cadastro, recebe o que o cliente enviou e o ignora; o elemento nulo não é recusado por
/// ela.
/// </remarks>
public sealed record DeclaracaoDaAvaliacaoDaEtapa(
    decimal? Peso,
    decimal? NotaMinima,
    decimal? NotaMaxima,
    decimal? PassoDoLancamento,
    FormaDeAvaliacao? FormaDeAvaliacao,
    ReferenciaRegra? RegraConsolidacao,
    IReadOnlyList<NivelDeAtendimento?>? EscalaDeAtendimento);
