namespace Unifesspa.UniPlus.Configuracao.Application.Queries.TiposDocumento;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

public static class ListarTiposDocumentoQueryHandler
{
    public static async Task<ListarTiposDocumentoResult> Handle(
        ListarTiposDocumentoQuery query,
        ITipoDocumentoRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        (IReadOnlyList<TipoDocumento> itens, Guid? anteriorAfterId, Guid? proximoAfterId) = await repository
            .ListarPaginadoAsync(query.AfterId, query.Limit, query.Direction, cancellationToken)
            .ConfigureAwait(false);

        TipoDocumentoDto[] items = [.. itens.Select(t => t.ToDto())];
        return new ListarTiposDocumentoResult(items, anteriorAfterId, proximoAfterId);
    }
}
