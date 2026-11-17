namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Queries.Unidades;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.DTOs;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Mappings;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;

/// <summary>
/// Handler convention-based de <see cref="ListarUnidadesAtivasQuery"/>: recusa a busca longa demais e o
/// tipo inválido (<see cref="FiltroDaListagemDeUnidades"/>, nessa ordem) antes de consultar o
/// repositório, e só então pagina por cursor bidirecional (ADR-0026 + ADR-0089). A mecânica de keyset
/// (ordenação, probe <c>n+1</c>, reversão e flags <c>prev</c>/<c>next</c> sem COUNT) vive no
/// repositório via <c>CursorKeyset</c>; a normalização acento/caixa é delegada ao banco via
/// immutable_unaccent + ILIKE.
/// </summary>
public static class ListarUnidadesAtivasQueryHandler
{
    public static async Task<Result<ListarUnidadesAtivasResult>> Handle(
        ListarUnidadesAtivasQuery query,
        IUnidadeRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        Result<FiltroListagemUnidades> filtro = FiltroDaListagemDeUnidades.Resolver(query.Busca, query.Tipos);

        return filtro.IsSuccess
            ? Result<ListarUnidadesAtivasResult>.Success(await PaginaAsync(query, filtro.Value!, repository, cancellationToken).ConfigureAwait(false))
            : Result<ListarUnidadesAtivasResult>.Failure(filtro.Error!);
    }

    private static async Task<ListarUnidadesAtivasResult> PaginaAsync(
        ListarUnidadesAtivasQuery query,
        FiltroListagemUnidades filtro,
        IUnidadeRepository repository,
        CancellationToken cancellationToken)
    {
        (IReadOnlyList<Unidade> itens, Guid? anteriorAfterId, Guid? proximoAfterId) = await repository
            .ListarPaginadoAsync(query.AfterId, query.Limit, query.Direction, filtro, cancellationToken)
            .ConfigureAwait(false);

        List<UnidadeDto> unidades = [.. itens.Select(u => u.ToDto())];
        return new ListarUnidadesAtivasResult(unidades, anteriorAfterId, proximoAfterId);
    }
}
