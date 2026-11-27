namespace Unifesspa.UniPlus.Selecao.Application.Queries.MotivosDeDecisao;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Mappings;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>Handler convention-based da listagem do cadastro; o keyset vive no repositório.</summary>
public static class ListarMotivosDeDecisaoQueryHandler
{
    public static async Task<ListarMotivosDeDecisaoResult> Handle(
        ListarMotivosDeDecisaoQuery query,
        IMotivoDeDecisaoRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        (IReadOnlyList<MotivoDeDecisao> itens, (string SortKey, Guid Id)? anterior, (string SortKey, Guid Id)? proximo) = await repository
            .ListarPaginadoAsync(query.AfterSortKey, query.AfterId, query.Limit, query.Direction, query.Filtro, cancellationToken)
            .ConfigureAwait(false);

        MotivoDeDecisaoDto[] items = [.. itens.Select(MotivoDeDecisaoMapping.ToDto)];
        return new ListarMotivosDeDecisaoResult(items, anterior, proximo);
    }
}
