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
        (IReadOnlyList<TipoEtapa> itens, Guid? anterior, Guid? proximo) = await repository
            .ListarPaginadoAsync(query.AfterId, query.Limit, query.Direction, cancellationToken).ConfigureAwait(false);
        TipoEtapaDto[] items = [.. itens.Select(tipo => tipo.ToDto())];
        return new ListarTiposEtapaResult(items, anterior, proximo);
    }
}
