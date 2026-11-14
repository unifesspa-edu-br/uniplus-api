namespace Unifesspa.UniPlus.Configuracao.Application.Queries.TiposConceito;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

public static class ListarTiposConceitoQueryHandler
{
    public static async Task<ListarTiposConceitoResult> Handle(
        ListarTiposConceitoQuery query,
        ITipoConceitoRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);
        (IReadOnlyList<TipoConceito> itens, Guid? anterior, Guid? proximo) = await repository
            .ListarPaginadoAsync(query.AfterId, query.Limit, query.Direction, cancellationToken).ConfigureAwait(false);
        TipoConceitoDto[] items = [.. itens.Select(tipo => tipo.ToDto())];
        return new ListarTiposConceitoResult(items, anterior, proximo);
    }
}
