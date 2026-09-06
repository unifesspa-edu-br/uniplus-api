namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>
/// Um tipo de banca do conjunto canônico (UNI-REQ-0139). Existe para que o cliente
/// descubra os códigos em runtime, em vez de manter uma cópia deles — uma cópia envelhece
/// sem avisar, e o drift só aparece como requisição recusada, do lado de quem preencheu o
/// formulário corretamente.
/// </summary>
public sealed record TipoBancaVocabularioDto(
    string Codigo,
    string Nome);

/// <summary>
/// Uma fase canônica do conjunto fechado (UNI-REQ-0139). Mesmo propósito de
/// <see cref="TipoBancaVocabularioDto"/>.
/// </summary>
public sealed record FaseCanonicaVocabularioDto(
    string Codigo,
    string Nome);
