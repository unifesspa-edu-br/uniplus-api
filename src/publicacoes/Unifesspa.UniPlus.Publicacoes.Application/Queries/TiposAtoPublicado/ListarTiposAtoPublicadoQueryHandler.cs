namespace Unifesspa.UniPlus.Publicacoes.Application.Queries.TiposAtoPublicado;

using Unifesspa.UniPlus.Publicacoes.Application.DTOs;
using Unifesspa.UniPlus.Publicacoes.Application.Mappings;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;

public static class ListarTiposAtoPublicadoQueryHandler
{
    public static async Task<ListarTiposAtoPublicadoResult> Handle(
        ListarTiposAtoPublicadoQuery query,
        ITipoAtoPublicadoRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        (IReadOnlyList<TipoAtoPublicado> itens, Guid? anteriorAfterId, Guid? proximoAfterId) = await repository
            .ListarPaginadoAsync(query.AfterId, query.Limit, query.Direction, cancellationToken)
            .ConfigureAwait(false);

        TipoAtoPublicadoDto[] items = [.. itens.Select(t => t.ToDto())];
        return new ListarTiposAtoPublicadoResult(items, anteriorAfterId, proximoAfterId);
    }
}
