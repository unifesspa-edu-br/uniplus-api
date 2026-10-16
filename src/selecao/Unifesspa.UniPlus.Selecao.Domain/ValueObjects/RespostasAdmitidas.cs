namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>O que a versão fixada do formulário admite como resposta da inscrição.</summary>
/// <param name="Definicoes">A definição da resposta de cada item, pelo código do fato.</param>
/// <param name="DoNivelSuperior">Os códigos de fato que a inscrição admite como resposta de nível superior, fora de qualquer grupo.</param>
/// <param name="SubitensPorGrupo">Os códigos de fato que cada ocorrência de um grupo repetível admite, pelo código do grupo.</param>
/// <param name="OfertasDeCurso">As ofertas de curso do processo, entre as quais o candidato escolhe.</param>
/// <param name="Termos">Os termos que o formulário de inscrição exige, pelo código do termo, cada um com a versão e o hash em que se aceita.</param>
/// <param name="NomeCivilDoLogin">O nome completo do token do candidato, contra o qual se confere a resposta NOME; ausente quando o token não o traz.</param>
public sealed record RespostasAdmitidas(
    IReadOnlyDictionary<string, DefinicaoDaResposta> Definicoes,
    IReadOnlySet<string> DoNivelSuperior,
    IReadOnlyDictionary<string, IReadOnlySet<string>> SubitensPorGrupo,
    IReadOnlySet<Guid> OfertasDeCurso,
    IReadOnlyDictionary<string, TermoAdmitido> Termos,
    string? NomeCivilDoLogin = null);
