namespace Unifesspa.UniPlus.Geo.Application.Queries.CidadeHierarquia;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Lista logradouros vigentes de uma Cidade vigente, paginados por cursor e
/// filtrados opcionalmente por busca textual.
/// </summary>
public sealed record ListarLogradourosQuery(
    string CodigoIbge,
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction,
    string? Busca) : IQuery<ListarLogradourosResult>;
