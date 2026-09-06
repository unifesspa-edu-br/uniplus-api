namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Cursos;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

public static class ListarCursosQueryHandler
{
    public static async Task<ListarCursosResult> Handle(
        ListarCursosQuery query,
        ICursoRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        (IReadOnlyList<Curso> itens, (string SortKey, Guid Id)? anterior, (string SortKey, Guid Id)? proximo) =
            await repository
                .ListarPaginadoAsync(query.AfterSortKey, query.AfterId, query.Limit, query.Direction, cancellationToken)
                .ConfigureAwait(false);

        CursoDto[] items = [.. itens.Select(c => c.ToDto())];
        return new ListarCursosResult(items, anterior, proximo);
    }
}
