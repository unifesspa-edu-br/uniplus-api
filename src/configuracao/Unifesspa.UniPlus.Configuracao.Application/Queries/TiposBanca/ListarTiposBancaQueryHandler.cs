namespace Unifesspa.UniPlus.Configuracao.Application.Queries.TiposBanca;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

public static class ListarTiposBancaQueryHandler
{
    public static async Task<ListarTiposBancaResult> Handle(
        ListarTiposBancaQuery query,
        ITipoBancaRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        (IReadOnlyList<TipoBanca> itens, Guid? anteriorAfterId, Guid? proximoAfterId) = await repository
            .ListarPaginadoAsync(query.AfterId, query.Limit, query.Direction, cancellationToken)
            .ConfigureAwait(false);

        TipoBancaDto[] items = [.. itens.Select(b => b.ToDto())];
        return new ListarTiposBancaResult(items, anteriorAfterId, proximoAfterId);
    }
}
