namespace Unifesspa.UniPlus.Configuracao.Application.Queries.TiposProcesso;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

public static class ListarTiposProcessoQueryHandler
{
    public static async Task<ListarTiposProcessoResult> Handle(
        ListarTiposProcessoQuery query,
        ITipoProcessoRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);
        (IReadOnlyList<TipoProcesso> itens, Guid? anterior, Guid? proximo) = await repository
            .ListarPaginadoAsync(query.AfterId, query.Limit, query.Direction, query.ApenasAtivos, cancellationToken).ConfigureAwait(false);
        TipoProcessoDto[] items = [.. itens.Select(tipo => tipo.ToDto())];
        return new ListarTiposProcessoResult(items, anterior, proximo);
    }
}
