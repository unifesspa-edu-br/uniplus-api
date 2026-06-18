namespace Unifesspa.UniPlus.Geo.Application.Queries.CidadeHierarquia;

using Unifesspa.UniPlus.Geo.Application.DTOs;

/// <summary>
/// Resultado da listagem/autocomplete de bairros de uma Cidade.
/// </summary>
public sealed record ListarBairrosResult(
    bool CidadeExiste,
    IReadOnlyList<BairroDto> Items,
    Guid? AnteriorAfterId,
    Guid? ProximoAfterId);
