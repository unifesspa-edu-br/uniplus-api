namespace Unifesspa.UniPlus.Configuracao.Application.Queries.ReferenciasReservaDemografica;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

public static class ListarReferenciasReservaDemograficaQueryHandler
{
    public static async Task<ListarReferenciasReservaDemograficaResult> Handle(
        ListarReferenciasReservaDemograficaQuery query,
        IReferenciaReservaDemograficaRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        (IReadOnlyList<ReferenciaReservaDemografica> itens, Guid? anteriorAfterId, Guid? proximoAfterId) = await repository
            .ListarPaginadoAsync(query.AfterId, query.Limit, query.Direction, cancellationToken)
            .ConfigureAwait(false);

        ReferenciaReservaDemograficaDto[] items = [.. itens.Select(r => r.ToDto())];
        return new ListarReferenciasReservaDemograficaResult(items, anteriorAfterId, proximoAfterId);
    }
}
