namespace Unifesspa.UniPlus.Configuracao.Application.Queries.PesosAreaEnem;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

public static class ListarPesosAreaEnemQueryHandler
{
    public static async Task<ListarPesosAreaEnemResult> Handle(
        ListarPesosAreaEnemQuery query,
        IPesoAreaEnemRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        (IReadOnlyList<PesoAreaEnem> itens, (string SortKey, Guid Id)? anterior, (string SortKey, Guid Id)? proximo) = await repository
            .ListarPaginadoAsync(query.AfterSortKey, query.AfterId, query.Limit, query.Direction, cancellationToken)
            .ConfigureAwait(false);

        PesoAreaEnemDto[] items = [.. itens.Select(p => p.ToDto())];
        return new ListarPesosAreaEnemResult(items, anterior, proximo);
    }
}
