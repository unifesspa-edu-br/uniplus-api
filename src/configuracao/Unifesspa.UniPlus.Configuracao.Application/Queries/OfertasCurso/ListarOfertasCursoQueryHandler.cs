namespace Unifesspa.UniPlus.Configuracao.Application.Queries.OfertasCurso;

using Unifesspa.UniPlus.Configuracao.Application.Consultas;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

public static class ListarOfertasCursoQueryHandler
{
    /// <summary>Ordem alfabética pelo curso ofertado, com o código dele como desempate.</summary>
    private static readonly IReadOnlyList<SortField> Padrao =
    [
        new(CamposOrdenacaoOfertaCurso.CursoNome, SortDirection.Ascending),
        new(CamposOrdenacaoOfertaCurso.CursoCodigo, SortDirection.Ascending),
    ];

    public static async Task<Result<ListarOfertasCursoResult>> Handle(
        ListarOfertasCursoQuery query,
        IOfertaCursoRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        Result<IReadOnlyList<SortField>> ordenacao = OrdenacaoPedida.Resolver(
            query.Ordenacao, CamposOrdenacaoOfertaCurso.Todos, Padrao);

        if (!ordenacao.IsSuccess)
        {
            return Result<ListarOfertasCursoResult>.Failure(ordenacao.Error!);
        }

        (IReadOnlyList<OfertaCurso> itens, (string SortKey, Guid Id)? anterior, (string SortKey, Guid Id)? proximo) =
            await repository
                .ListarPaginadoAsync(
                    ordenacao.Value!,
                    query.Busca,
                    query.AfterSortKey,
                    query.AfterId,
                    query.Limit,
                    query.Direction,
                    query.CursoId,
                    cancellationToken)
                .ConfigureAwait(false);

        OfertaCursoDto[] items = [.. itens.Select(o => o.ToDto())];
        return Result<ListarOfertasCursoResult>.Success(
            new ListarOfertasCursoResult(items, anterior, proximo));
    }
}
