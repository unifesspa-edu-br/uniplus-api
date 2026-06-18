namespace Unifesspa.UniPlus.Geo.Application.Queries.CidadeHierarquia;

using Unifesspa.UniPlus.Geo.Application.DTOs;

/// <summary>
/// Resultado da listagem/autocomplete de logradouros de uma Cidade.
/// </summary>
public sealed record ListarLogradourosResult(
    bool CidadeExiste,
    IReadOnlyList<LogradouroResumoDto> Items,
    Guid? AnteriorAfterId,
    Guid? ProximoAfterId);
