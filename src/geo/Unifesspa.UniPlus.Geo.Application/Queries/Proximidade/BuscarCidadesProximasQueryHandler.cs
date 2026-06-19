namespace Unifesspa.UniPlus.Geo.Application.Queries.Proximidade;

using Unifesspa.UniPlus.Geo.Application.Abstractions;
using Unifesspa.UniPlus.Geo.Application.DTOs;

/// <summary>
/// Handler convention-based de <see cref="BuscarCidadesProximasQuery"/>: delega ao
/// <see cref="IGeoProximidadeReader"/> (filtro <c>ST_DWithin</c> + ordenação
/// <c>ST_Distance</c>). Os <c>_links</c> são montados no controller.
/// </summary>
public static class BuscarCidadesProximasQueryHandler
{
    public static async Task<IReadOnlyList<CidadeProximaDto>> Handle(
        BuscarCidadesProximasQuery query,
        IGeoProximidadeReader reader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(reader);

        return await reader
            .BuscarCidadesProximasAsync(query.Latitude, query.Longitude, query.RaioKm, query.Limit, cancellationToken)
            .ConfigureAwait(false);
    }
}
