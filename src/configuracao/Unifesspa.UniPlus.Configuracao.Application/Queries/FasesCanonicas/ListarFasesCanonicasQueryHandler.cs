namespace Unifesspa.UniPlus.Configuracao.Application.Queries.FasesCanonicas;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

public static class ListarFasesCanonicasQueryHandler
{
    public static async Task<ListarFasesCanonicasResult> Handle(
        ListarFasesCanonicasQuery query,
        IFaseCanonicaRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        (IReadOnlyList<FaseCanonica> itens, Guid? anteriorAfterId, Guid? proximoAfterId) = await repository
            .ListarPaginadoAsync(query.AfterId, query.Limit, query.Direction, cancellationToken)
            .ConfigureAwait(false);

        FaseCanonicaDto[] items = [.. itens.Select(f => f.ToDto())];
        return new ListarFasesCanonicasResult(items, anteriorAfterId, proximoAfterId);
    }
}
