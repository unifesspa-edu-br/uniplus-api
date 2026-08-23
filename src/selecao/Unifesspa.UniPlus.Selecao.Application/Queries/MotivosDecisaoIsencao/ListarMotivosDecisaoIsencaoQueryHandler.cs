namespace Unifesspa.UniPlus.Selecao.Application.Queries.MotivosDecisaoIsencao;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Mappings;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Handler convention-based da listagem do catálogo. A mecânica de keyset vive
/// no repositório; o handler apenas projeta em DTO.
/// </summary>
public static class ListarMotivosDecisaoIsencaoQueryHandler
{
    public static async Task<ListarMotivosDecisaoIsencaoResult> Handle(
        ListarMotivosDecisaoIsencaoQuery query,
        IMotivoDecisaoIsencaoRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        (IReadOnlyList<MotivoDecisaoIsencao> itens, Guid? anteriorAfterId, Guid? proximoAfterId) =
            await repository.ListarPaginadoAsync(
                query.AfterId,
                query.Limit,
                query.Direction,
                query.Fundamento,
                query.ApenasAtivos,
                cancellationToken).ConfigureAwait(false);

        MotivoDecisaoIsencaoDto[] items = [.. itens.Select(MotivoDecisaoIsencaoMapping.ToDto)];

        return new ListarMotivosDecisaoIsencaoResult(items, anteriorAfterId, proximoAfterId);
    }
}
