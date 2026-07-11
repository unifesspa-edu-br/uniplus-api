namespace Unifesspa.UniPlus.Publicacoes.Application.Queries.AtosNormativos;

using Unifesspa.UniPlus.Publicacoes.Application.DTOs;

/// <summary>
/// Página de atos de uma entidade. As âncoras são o par <c>(data de publicação, Id)</c>
/// do primeiro e do último item (ADR-0094); nulas quando não há aquele lado.
/// </summary>
public sealed record ListarAtosDaEntidadeResult(
    IReadOnlyList<AtoNormativoDto> Items,
    (string SortKey, Guid Id)? Anterior,
    (string SortKey, Guid Id)? Proximo);
