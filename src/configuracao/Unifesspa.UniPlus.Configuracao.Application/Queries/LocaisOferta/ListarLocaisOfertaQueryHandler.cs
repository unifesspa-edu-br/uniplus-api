namespace Unifesspa.UniPlus.Configuracao.Application.Queries.LocaisOferta;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

public static class ListarLocaisOfertaQueryHandler
{
    public static async Task<ListarLocaisOfertaResult> Handle(
        ListarLocaisOfertaQuery query,
        ILocalOfertaRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        (IReadOnlyList<LocalOferta> itens, (string SortKey, Guid Id)? anterior, (string SortKey, Guid Id)? proximo) = await repository
            .ListarPaginadoAsync(query.AfterSortKey, query.AfterId, query.Limit, query.Direction, cancellationToken)
            .ConfigureAwait(false);

        LocalOfertaDto[] items = [.. itens.Select(l => l.ToDto())];
        return new ListarLocaisOfertaResult(items, anterior, proximo);
    }
}
