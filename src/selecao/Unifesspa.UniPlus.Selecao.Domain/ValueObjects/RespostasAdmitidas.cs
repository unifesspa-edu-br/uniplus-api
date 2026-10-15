namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>O que a versão fixada do formulário admite como resposta da inscrição.</summary>
/// <param name="Definicoes">A definição da resposta de cada item, pelo código do fato.</param>
/// <param name="SubitensPorGrupo">Os códigos de fato que cada ocorrência de um grupo repetível admite, pelo código do grupo.</param>
/// <param name="OfertasDeCurso">As ofertas de curso do processo, entre as quais o candidato escolhe.</param>
public sealed record RespostasAdmitidas(
    IReadOnlyDictionary<string, DefinicaoDaResposta> Definicoes,
    IReadOnlyDictionary<string, IReadOnlySet<string>> SubitensPorGrupo,
    IReadOnlySet<Guid> OfertasDeCurso);
