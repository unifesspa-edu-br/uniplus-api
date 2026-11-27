namespace Unifesspa.UniPlus.Configuracao.Application.Queries.TiposEtapa;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

public static class ListarTiposEtapaQueryHandler
{
    public static async Task<ListarTiposEtapaResult> Handle(
        ListarTiposEtapaQuery query,
        ITipoEtapaRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);
        (IReadOnlyList<TipoEtapa> itens, (string SortKey, Guid Id)? anterior, (string SortKey, Guid Id)? proximo) = await repository
            .ListarPaginadoAsync(query.AfterSortKey, query.AfterId, query.Limit, query.Direction, cancellationToken).ConfigureAwait(false);
        TipoEtapaDto[] items = [.. itens.Select(tipo => tipo.ToDto())];
        return new ListarTiposEtapaResult(items, anterior, proximo);
    }
}
