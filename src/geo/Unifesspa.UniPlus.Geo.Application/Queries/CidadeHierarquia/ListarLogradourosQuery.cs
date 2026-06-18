namespace Unifesspa.UniPlus.Geo.Application.Queries.CidadeHierarquia;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Lista logradouros vigentes de uma Cidade vigente. Sem <see cref="Busca"/>, paginação
/// por cursor (navegação). Com <see cref="Busca"/>, autocomplete: casa o texto completo
/// (tipo + nome) e ordena por relevância (similaridade), top-N sem cursor.
/// </summary>
public sealed record ListarLogradourosQuery(
    string CodigoIbge,
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction,
    string? Busca) : IQuery<ListarLogradourosResult>;
