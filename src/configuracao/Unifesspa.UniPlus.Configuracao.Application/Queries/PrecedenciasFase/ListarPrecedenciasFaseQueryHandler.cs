namespace Unifesspa.UniPlus.Configuracao.Application.Queries.PrecedenciasFase;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

public static class ListarPrecedenciasFaseQueryHandler
{
    public static async Task<ListarPrecedenciasFaseResult> Handle(
        ListarPrecedenciasFaseQuery query,
        IPrecedenciaFaseRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        (IReadOnlyList<PrecedenciaFase> itens, Guid? anteriorAfterId, Guid? proximoAfterId) = await repository
            .ListarPaginadoAsync(query.AfterId, query.Limit, query.Direction, cancellationToken)
            .ConfigureAwait(false);

        PrecedenciaFaseDto[] items = [.. itens.Select(p => p.ToDto())];
        return new ListarPrecedenciasFaseResult(items, anteriorAfterId, proximoAfterId);
    }
}
