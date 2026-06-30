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

        (IReadOnlyList<TipoDeficiencia> itens, Guid? anteriorAfterId, Guid? proximoAfterId) = await repository
            .ListarPaginadoAsync(query.AfterId, query.Limit, query.Direction, cancellationToken)
            .ConfigureAwait(false);

        TipoDeficienciaDto[] items = [.. itens.Select(t => t.ToDto())];
        return new ListarTiposDeficienciaResult(items, anteriorAfterId, proximoAfterId);
    }
}
