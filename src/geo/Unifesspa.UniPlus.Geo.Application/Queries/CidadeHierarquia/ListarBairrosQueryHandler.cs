namespace Unifesspa.UniPlus.Geo.Application.Queries.CidadeHierarquia;

using Unifesspa.UniPlus.Geo.Application.Abstractions;
using Unifesspa.UniPlus.Geo.Application.DTOs;
using Unifesspa.UniPlus.Geo.Application.Mappings;
using Unifesspa.UniPlus.Geo.Domain.Entities;

public static class ListarBairrosQueryHandler
{
    public static async Task<ListarBairrosResult> Handle(
        ListarBairrosQuery query,
        IBairroReader reader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(reader);

        string? busca = string.IsNullOrWhiteSpace(query.Busca) ? null : query.Busca;

        (bool cidadeExiste, IReadOnlyList<Bairro> itens, Guid? anteriorAfterId, Guid? proximoAfterId) =
            await reader
                .ListarPorCidadeAsync(
                    query.CodigoIbge, query.AfterId, query.Limit, query.Direction, busca, cancellationToken)
                .ConfigureAwait(false);

        if (!cidadeExiste)
        {
            return new ListarBairrosResult(false, [], anteriorAfterId, proximoAfterId);
        }

        BairroDto[] items = [.. itens.Select(b => b.ToDto(query.CodigoIbge))];
        return new ListarBairrosResult(true, items, anteriorAfterId, proximoAfterId);
    }
}
