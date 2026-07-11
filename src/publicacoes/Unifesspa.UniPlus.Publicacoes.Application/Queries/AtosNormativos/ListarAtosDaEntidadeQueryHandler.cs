namespace Unifesspa.UniPlus.Publicacoes.Application.Queries.AtosNormativos;

using Unifesspa.UniPlus.Publicacoes.Application.DTOs;
using Unifesspa.UniPlus.Publicacoes.Application.Mappings;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;

public static class ListarAtosDaEntidadeQueryHandler
{
    public static async Task<ListarAtosDaEntidadeResult> Handle(
        ListarAtosDaEntidadeQuery query,
        IAtoNormativoRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        (IReadOnlyList<AtoNormativo> itens, (string SortKey, Guid Id)? anterior, (string SortKey, Guid Id)? proximo) =
            await repository
                .ListarPorEntidadeAsync(
                    query.EntidadeTipo,
                    query.EntidadeId,
                    query.AfterSortKey,
                    query.AfterId,
                    query.Limit,
                    query.Direction,
                    cancellationToken)
                .ConfigureAwait(false);

        AtoNormativoDto[] items = [.. itens.Select(a => a.ToDto())];
        return new ListarAtosDaEntidadeResult(items, anterior, proximo);
    }
}
