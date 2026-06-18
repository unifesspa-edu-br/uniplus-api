namespace Unifesspa.UniPlus.Geo.Application.Queries.CidadeHierarquia;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Lista distritos vigentes de uma Cidade vigente, paginados por cursor.
/// </summary>
public sealed record ListarDistritosQuery(
    string CodigoIbge,
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction) : IQuery<ListarDistritosResult>;
