namespace Unifesspa.UniPlus.Configuracao.Application.Queries.RecursosAcessibilidade;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

public static class ListarRecursosAcessibilidadeQueryHandler
{
    public static async Task<ListarRecursosAcessibilidadeResult> Handle(
        ListarRecursosAcessibilidadeQuery query,
        IRecursoAcessibilidadeRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        (IReadOnlyList<RecursoAcessibilidade> itens, Guid? anteriorAfterId, Guid? proximoAfterId) = await repository
            .ListarPaginadoAsync(query.AfterId, query.Limit, query.Direction, cancellationToken)
            .ConfigureAwait(false);

        RecursoAcessibilidadeDto[] items = [.. itens.Select(r => r.ToDto())];
        return new ListarRecursosAcessibilidadeResult(items, anteriorAfterId, proximoAfterId);
    }
}
