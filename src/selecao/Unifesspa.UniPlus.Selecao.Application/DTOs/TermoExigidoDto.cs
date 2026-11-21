namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// A obrigatoriedade de um termo: <c>SEMPRE</c>, <c>NUNCA</c> ou <c>QUANDO</c>, com o predicado só
/// em <c>QUANDO</c>, na mesma forma flat da pré-condição de fato coletado.
/// </summary>
public sealed record ObrigatoriedadeDto(string Tipo, IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoDto>>? Predicado);

/// <summary>
/// Quando um grupo repetível ou um termo aparece — a exibição, nula quando sempre aparece — e quando é
/// obrigatório. Tem a forma de <see cref="Unifesspa.UniPlus.Regras.Entradas.ExibicaoEObrigatoriedadeInput"/>, que a escrita aceita.
/// </summary>
public sealed record ExibicaoEObrigatoriedadeDto(
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoDto>>? Exibicao,
    ObrigatoriedadeDto Obrigatoriedade);

/// <summary>
/// Um termo exigido pelo formulário (UNI-REQ-0086): a exigência, a versão escolhida no catálogo
/// com o conteúdo congelado, e as condições de exibição (nula quando sempre aparece) e de
/// obrigatoriedade.
/// </summary>
public sealed record TermoExigidoDto(
    string Codigo,
    int Ordem,
    Guid TermoId,
    Guid VersaoId,
    string TermoCodigo,
    int VersaoNumero,
    string Nome,
    string Texto,
    string BaseLegal,
    string FormaAceite,
    string HashVersao,
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoDto>>? Exibicao,
    ObrigatoriedadeDto Obrigatoriedade);
