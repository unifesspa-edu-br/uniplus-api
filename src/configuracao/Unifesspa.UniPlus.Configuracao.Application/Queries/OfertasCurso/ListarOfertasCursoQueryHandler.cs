namespace Unifesspa.UniPlus.Configuracao.Application.Queries.OfertasCurso;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

public static class ListarOfertasCursoQueryHandler
{
    public static async Task<ListarOfertasCursoResult> Handle(
        ListarOfertasCursoQuery query,
        IOfertaCursoRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        (IReadOnlyList<OfertaCurso> itens, Guid? anteriorAfterId, Guid? proximoAfterId) = await repository
            .ListarPaginadoAsync(query.AfterId, query.Limit, query.Direction, query.CursoId, cancellationToken)
            .ConfigureAwait(false);

        OfertaCursoDto[] items = [.. itens.Select(o => o.ToDto())];
        return new ListarOfertasCursoResult(items, anteriorAfterId, proximoAfterId);
    }
}
