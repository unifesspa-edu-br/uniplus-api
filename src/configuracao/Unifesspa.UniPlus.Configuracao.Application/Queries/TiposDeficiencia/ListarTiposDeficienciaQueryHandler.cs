namespace Unifesspa.UniPlus.Configuracao.Application.Queries.TiposDeficiencia;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

public static class ListarTiposDeficienciaQueryHandler
{
    public static async Task<ListarTiposDeficienciaResult> Handle(
        ListarTiposDeficienciaQuery query,
        ITipoDeficienciaRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        (IReadOnlyList<TipoDeficiencia> itens, (string SortKey, Guid Id)? anterior, (string SortKey, Guid Id)? proximo) = await repository
            .ListarPaginadoAsync(query.AfterSortKey, query.AfterId, query.Limit, query.Direction, cancellationToken)
            .ConfigureAwait(false);

        TipoDeficienciaDto[] items = [.. itens.Select(t => t.ToDto())];
        return new ListarTiposDeficienciaResult(items, anterior, proximo);
    }
}
