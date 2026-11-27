namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Modalidades;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

public static class ListarModalidadesQueryHandler
{
    public static async Task<ListarModalidadesResult> Handle(
        ListarModalidadesQuery query,
        IModalidadeRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        (IReadOnlyList<Modalidade> itens, (string SortKey, Guid Id)? anterior, (string SortKey, Guid Id)? proximo) = await repository
            .ListarPaginadoAsync(query.AfterSortKey, query.AfterId, query.Limit, query.Direction, cancellationToken)
            .ConfigureAwait(false);

        ModalidadeDto[] items = [.. itens.Select(m => m.ToDto())];
        return new ListarModalidadesResult(items, anterior, proximo);
    }
}
