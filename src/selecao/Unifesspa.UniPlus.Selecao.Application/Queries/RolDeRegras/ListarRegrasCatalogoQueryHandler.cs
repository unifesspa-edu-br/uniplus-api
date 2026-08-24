namespace Unifesspa.UniPlus.Selecao.Application.Queries.RolDeRegras;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Mappings;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Handler convention-based da listagem do catálogo. A mecânica de keyset vive no reader — o
/// mesmo <see cref="IRegraCatalogoReader"/> que as dimensões da configuração já usam para
/// resolver a regra que referenciam, sem segunda via de acesso ao catálogo.
/// </summary>
public static class ListarRegrasCatalogoQueryHandler
{
    public static async Task<ListarRegrasCatalogoResult> Handle(
        ListarRegrasCatalogoQuery query,
        IRegraCatalogoReader reader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(reader);

        (IReadOnlyList<RegraCatalogo> itens, Guid? anteriorAfterId, Guid? proximoAfterId) =
            await reader.ListarPaginadoAsync(
                query.Tipo,
                query.AfterId,
                query.Limit,
                query.Direction,
                cancellationToken).ConfigureAwait(false);

        RegraCatalogoDto[] items = [.. itens.Select(RegraCatalogoMapping.ToDto)];

        return new ListarRegrasCatalogoResult(items, anteriorAfterId, proximoAfterId);
    }
}
