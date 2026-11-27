namespace Unifesspa.UniPlus.Configuracao.Application.Queries.PrecedenciasFase;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

public static class ListarPrecedenciasFaseQueryHandler
{
    public static async Task<ListarPrecedenciasFaseResult> Handle(
        ListarPrecedenciasFaseQuery query,
        IPrecedenciaFaseRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        (IReadOnlyList<PrecedenciaFase> itens, (string SortKey, Guid Id)? anterior, (string SortKey, Guid Id)? proximo) = await repository
            .ListarPaginadoAsync(query.AfterSortKey, query.AfterId, query.Limit, query.Direction, cancellationToken)
            .ConfigureAwait(false);

        PrecedenciaFaseDto[] items = [.. itens.Select(p => p.ToDto())];
        return new ListarPrecedenciasFaseResult(items, anterior, proximo);
    }
}
