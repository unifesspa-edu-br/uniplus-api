namespace Unifesspa.UniPlus.Configuracao.Application.Queries.PesosAreaEnem;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

public static class ListarPesosAreaEnemQueryHandler
{
    public static async Task<ListarPesosAreaEnemResult> Handle(
        ListarPesosAreaEnemQuery query,
        IPesoAreaEnemRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        (IReadOnlyList<PesoAreaEnem> itens, Guid? anteriorAfterId, Guid? proximoAfterId) = await repository
            .ListarPaginadoAsync(query.AfterId, query.Limit, query.Direction, cancellationToken)
            .ConfigureAwait(false);

        PesoAreaEnemDto[] items = [.. itens.Select(p => p.ToDto())];
        return new ListarPesosAreaEnemResult(items, anteriorAfterId, proximoAfterId);
    }
}
