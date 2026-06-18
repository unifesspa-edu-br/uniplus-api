namespace Unifesspa.UniPlus.Geo.Application.Queries.Estados;

using Unifesspa.UniPlus.Geo.Application.Abstractions;
using Unifesspa.UniPlus.Geo.Application.DTOs;
using Unifesspa.UniPlus.Geo.Application.Mappings;
using Unifesspa.UniPlus.Geo.Domain.Entities;

/// <summary>
/// Handler convention-based de <see cref="ListarEstadosQuery"/>: paginação keyset
/// bidirecional (cursor) sobre <c>Estado</c>. A mecânica de keyset vive no reader
/// via <c>CursorKeyset</c>; o handler apenas projeta as entidades vigentes em DTO.
/// </summary>
public static class ListarEstadosQueryHandler
{
    public static async Task<ListarEstadosResult> Handle(
        ListarEstadosQuery query,
        IEstadoReader reader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(reader);

        (IReadOnlyList<Estado> itens, Guid? anteriorAfterId, Guid? proximoAfterId) = await reader
            .ListarPaginadoAsync(query.AfterId, query.Limit, query.Direction, cancellationToken)
            .ConfigureAwait(false);

        EstadoDto[] items = [.. itens.Select(e => e.ToDto())];
        return new ListarEstadosResult(items, anteriorAfterId, proximoAfterId);
    }
}
