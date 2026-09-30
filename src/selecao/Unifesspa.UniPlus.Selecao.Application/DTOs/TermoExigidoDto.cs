namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// A obrigatoriedade de um termo: <c>SEMPRE</c>, <c>NUNCA</c> ou <c>QUANDO</c>, com o predicado só
/// em <c>QUANDO</c>, na mesma forma flat da pré-condição de fato coletado.
/// </summary>
public sealed record ObrigatoriedadeDto(string Tipo, IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoDto>>? Predicado);

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
    string Nome,
    string Texto,
    string BaseLegal,
    string FormaAceite,
    string HashVersao,
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoDto>>? Exibicao,
    ObrigatoriedadeDto Obrigatoriedade);
