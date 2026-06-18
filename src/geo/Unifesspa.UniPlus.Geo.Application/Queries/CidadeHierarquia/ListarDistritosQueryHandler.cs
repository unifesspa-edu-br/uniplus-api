namespace Unifesspa.UniPlus.Geo.Application.Queries.CidadeHierarquia;

using Unifesspa.UniPlus.Geo.Application.Abstractions;
using Unifesspa.UniPlus.Geo.Application.DTOs;
using Unifesspa.UniPlus.Geo.Application.Mappings;
using Unifesspa.UniPlus.Geo.Domain.Entities;

public static class ListarDistritosQueryHandler
{
    public static async Task<ListarDistritosResult> Handle(
        ListarDistritosQuery query,
        IDistritoReader reader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(reader);

        (bool cidadeExiste, IReadOnlyList<Distrito> itens, Guid? anteriorAfterId, Guid? proximoAfterId) =
            await reader
                .ListarPorCidadeAsync(query.CodigoIbge, query.AfterId, query.Limit, query.Direction, cancellationToken)
                .ConfigureAwait(false);

        if (!cidadeExiste)
        {
            return new ListarDistritosResult(false, [], anteriorAfterId, proximoAfterId);
        }

        DistritoDto[] items = [.. itens.Select(d => d.ToDto(query.CodigoIbge))];
        return new ListarDistritosResult(true, items, anteriorAfterId, proximoAfterId);
    }
}
