namespace Unifesspa.UniPlus.Configuracao.Application.Queries.OfertasCurso;

using Unifesspa.UniPlus.Application.Abstractions.Consultas;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
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

        Result<TermoDeBusca?> busca = TermoDeBusca.Criar(query.Busca);
        Result<IReadOnlyList<SortField>> ordenacao = OrdenacaoPedida.Resolver(
            query.Ordenacao, CamposOrdenacaoOfertaCurso.Todos, Padrao);
        if (!busca.IsSuccess || !ordenacao.IsSuccess)
        {
            return Result<ListarOfertasCursoResult>.Failure((busca.IsSuccess ? ordenacao.Error : busca.Error)!);
        }

        var filtro = new FiltroDasOfertasDeCurso(busca.Value, query.CursoId);
        return await ListarPaginaAsync(query, repository, ordenacao.Value!, filtro, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<ListarOfertasCursoResult>> ListarPaginaAsync(
        ListarOfertasCursoQuery query,
        IOfertaCursoRepository repository,
        IReadOnlyList<SortField> ordenacao,
        FiltroDasOfertasDeCurso filtro,
        CancellationToken cancellationToken)
    {
        int? total = query.IncludeTotal ? await repository.ContarAsync(filtro, cancellationToken).ConfigureAwait(false) : null;
        (IReadOnlyList<OfertaCurso> itens, (string SortKey, Guid Id)? anterior, (string SortKey, Guid Id)? proximo) =
            await repository.ListarPaginadoAsync(
                ordenacao, filtro, query.AfterSortKey, query.AfterId, query.Limit, query.Direction, cancellationToken)
                .ConfigureAwait(false);
        OfertaCursoDto[] items = [.. itens.Select(o => o.ToDto())];
        return Result<ListarOfertasCursoResult>.Success(new ListarOfertasCursoResult(items, anterior, proximo, total));
    }
}
