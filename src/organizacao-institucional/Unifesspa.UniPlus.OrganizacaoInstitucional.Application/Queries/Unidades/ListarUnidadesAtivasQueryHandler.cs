namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Queries.Unidades;

using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.DTOs;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Mappings;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;

/// <summary>
/// Handler convention-based de <see cref="ListarUnidadesAtivasQuery"/>:
/// paginação keyset bidirecional (cursor) sobre o agregado <c>Unidade</c>
/// (ADR-0026 + ADR-0089). A mecânica de keyset (ordenação, probe <c>n+1</c>,
/// reversão e flags <c>prev</c>/<c>next</c> sem COUNT) vive no repositório via
/// <c>CursorKeyset</c>; o handler apenas monta o filtro (busca + tipos, issue
/// #640) e projeta as entidades em DTO. A normalização acento/caixa é delegada
/// ao banco via immutable_unaccent + ILIKE.
/// </summary>
public static class ListarUnidadesAtivasQueryHandler
{
    public static async Task<ListarUnidadesAtivasResult> Handle(
        ListarUnidadesAtivasQuery query,
        IUnidadeRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        FiltroListagemUnidades filtro = new(
            string.IsNullOrWhiteSpace(query.Busca) ? null : query.Busca,
            query.Tipos ?? []);

        (IReadOnlyList<Unidade> itens, Guid? anteriorAfterId, Guid? proximoAfterId) = await repository
            .ListarPaginadoAsync(query.AfterId, query.Limit, query.Direction, filtro, cancellationToken)
            .ConfigureAwait(false);

        UnidadeDto[] items = [.. itens.Select(u => u.ToDto())];
        return new ListarUnidadesAtivasResult(items, anteriorAfterId, proximoAfterId);
    }
}
