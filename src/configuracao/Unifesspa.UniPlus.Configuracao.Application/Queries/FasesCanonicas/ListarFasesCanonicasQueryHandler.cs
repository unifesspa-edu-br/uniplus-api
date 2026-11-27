namespace Unifesspa.UniPlus.Configuracao.Application.Queries.FasesCanonicas;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

public static class ListarFasesCanonicasQueryHandler
{
    public static async Task<ListarFasesCanonicasResult> Handle(
        ListarFasesCanonicasQuery query,
        IFaseCanonicaRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        (IReadOnlyList<FaseCanonica> itens, (string SortKey, Guid Id)? anterior, (string SortKey, Guid Id)? proximo) = await repository
            .ListarPaginadoAsync(query.AfterSortKey, query.AfterId, query.Limit, query.Direction, cancellationToken)
            .ConfigureAwait(false);

        FaseCanonicaDto[] items = [.. itens.Select(f => f.ToDto())];
        return new ListarFasesCanonicasResult(items, anterior, proximo);
    }
}
