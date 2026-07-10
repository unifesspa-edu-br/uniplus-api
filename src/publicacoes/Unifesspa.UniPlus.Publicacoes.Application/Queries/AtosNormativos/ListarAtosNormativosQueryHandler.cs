namespace Unifesspa.UniPlus.Publicacoes.Application.Queries.AtosNormativos;

using Unifesspa.UniPlus.Publicacoes.Application.DTOs;
using Unifesspa.UniPlus.Publicacoes.Application.Mappings;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;

public static class ListarAtosNormativosQueryHandler
{
    public static async Task<ListarAtosNormativosResult> Handle(
        ListarAtosNormativosQuery query,
        IAtoNormativoRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        (IReadOnlyList<AtoNormativo> itens, Guid? anteriorAfterId, Guid? proximoAfterId) = await repository
            .ListarPaginadoAsync(query.AfterId, query.Limit, query.Direction, cancellationToken)
            .ConfigureAwait(false);

        AtoNormativoDto[] items = [.. itens.Select(a => a.ToDto())];
        return new ListarAtosNormativosResult(items, anteriorAfterId, proximoAfterId);
    }
}
