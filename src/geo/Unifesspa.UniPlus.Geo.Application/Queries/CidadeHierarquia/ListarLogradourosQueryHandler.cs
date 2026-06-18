namespace Unifesspa.UniPlus.Geo.Application.Queries.CidadeHierarquia;

using Unifesspa.UniPlus.Geo.Application.Abstractions;
using Unifesspa.UniPlus.Geo.Application.DTOs;
using Unifesspa.UniPlus.Geo.Application.Mappings;

public static class ListarLogradourosQueryHandler
{
    public static async Task<ListarLogradourosResult> Handle(
        ListarLogradourosQuery query,
        ILogradouroReader reader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(reader);

        string? busca = string.IsNullOrWhiteSpace(query.Busca) ? null : query.Busca;

        (bool cidadeExiste, IReadOnlyList<LogradouroComBairro> itens, Guid? anteriorAfterId, Guid? proximoAfterId) =
            await reader
                .ListarPorCidadeAsync(
                    query.CodigoIbge, query.AfterId, query.Limit, query.Direction, busca, cancellationToken)
                .ConfigureAwait(false);

        if (!cidadeExiste)
        {
            return new ListarLogradourosResult(false, [], anteriorAfterId, proximoAfterId);
        }

        LogradouroResumoDto[] items = [.. itens.Select(l => l.ToDto(query.CodigoIbge))];
        return new ListarLogradourosResult(true, items, anteriorAfterId, proximoAfterId);
    }
}
