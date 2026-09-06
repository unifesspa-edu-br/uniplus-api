namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Cursos;

using Unifesspa.UniPlus.Configuracao.Application.Consultas;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

public static class ListarCursosQueryHandler
{
    /// <summary>Ordem alfabética de nome com o código como desempate.</summary>
    private static readonly IReadOnlyList<SortField> Padrao =
    [
        new(CamposOrdenacaoCurso.Nome, SortDirection.Ascending),
        new(CamposOrdenacaoCurso.Codigo, SortDirection.Ascending),
    ];

    public static async Task<Result<ListarCursosResult>> Handle(
        ListarCursosQuery query,
        ICursoRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        Result<IReadOnlyList<SortField>> ordenacao = OrdenacaoPedida.Resolver(
            query.Ordenacao, CamposOrdenacaoCurso.Todos, Padrao);

        if (!ordenacao.IsSuccess)
        {
            return Result<ListarCursosResult>.Failure(ordenacao.Error!);
        }

        (IReadOnlyList<Curso> itens, (string SortKey, Guid Id)? anterior, (string SortKey, Guid Id)? proximo) =
            await repository
                .ListarPaginadoAsync(
                    ordenacao.Value!,
                    query.Busca,
                    query.AfterSortKey,
                    query.AfterId,
                    query.Limit,
                    query.Direction,
                    cancellationToken)
                .ConfigureAwait(false);

        CursoDto[] items = [.. itens.Select(c => c.ToDto())];
        return Result<ListarCursosResult>.Success(new ListarCursosResult(items, anterior, proximo));
    }
}
