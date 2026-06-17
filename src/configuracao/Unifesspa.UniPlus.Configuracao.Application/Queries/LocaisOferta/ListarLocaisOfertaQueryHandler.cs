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

        (IReadOnlyList<LocalOferta> itens, Guid? anteriorAfterId, Guid? proximoAfterId) = await repository
            .ListarPaginadoAsync(query.AfterId, query.Limit, query.Direction, cancellationToken)
            .ConfigureAwait(false);

        LocalOfertaDto[] items = [.. itens.Select(l => l.ToDto())];
        return new ListarLocaisOfertaResult(items, anteriorAfterId, proximoAfterId);
    }
}
