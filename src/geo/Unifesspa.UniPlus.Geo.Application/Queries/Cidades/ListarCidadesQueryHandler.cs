namespace Unifesspa.UniPlus.Geo.Application.Queries.Cidades;

using Unifesspa.UniPlus.Geo.Application.Abstractions;
using Unifesspa.UniPlus.Geo.Application.DTOs;
using Unifesspa.UniPlus.Geo.Application.Mappings;
using Unifesspa.UniPlus.Geo.Domain.Entities;

/// <summary>
/// Handler convention-based de <see cref="ListarCidadesQuery"/>: monta o
/// <see cref="FiltroListagemCidades"/> (UF + busca) e delega ao reader a paginação
/// keyset bidirecional ordenada por nome sobre <c>Cidade</c>. A normalização
/// acento/caixa da busca é feita no reader (termo normalizado no app + <c>ILIKE</c>
/// sobre <c>nome_normalizado</c>). Projeta as entidades vigentes em DTO de resumo.
/// </summary>
public static class ListarCidadesQueryHandler
{
    public static async Task<ListarCidadesResult> Handle(
        ListarCidadesQuery query,
        ICidadeReader reader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(reader);

        FiltroListagemCidades filtro = new(
            string.IsNullOrWhiteSpace(query.Uf) ? null : query.Uf,
            string.IsNullOrWhiteSpace(query.Busca) ? null : query.Busca);

        (IReadOnlyList<Cidade> itens, (string SortKey, Guid Id)? anterior, (string SortKey, Guid Id)? proximo) = await reader
            .ListarPaginadoAsync(query.AfterSortKey, query.AfterId, query.Limit, query.Direction, filtro, cancellationToken)
            .ConfigureAwait(false);

        CidadeResumoDto[] items = [.. itens.Select(c => c.ToResumoDto())];
        return new ListarCidadesResult(items, anterior, proximo);
    }
}
