namespace Unifesspa.UniPlus.Selecao.Application.Queries.Editais;

using DTOs;
using Domain.Entities;
using Domain.Interfaces;

/// <summary>
/// Handler convention-based de <see cref="ListarEditaisQuery"/>: paginação
/// keyset bidirecional (cursor) sobre o agregado <c>Edital</c> (ADR-0026 +
/// ADR-0089). A mecânica de keyset (ordenação, probe <c>n+1</c>, reversão e
/// flags <c>prev</c>/<c>next</c> sem COUNT) vive no repositório via
/// <c>CursorKeyset</c>; o handler apenas projeta as entidades em DTO.
/// </summary>
public static class ListarEditaisQueryHandler
{
    public static async Task<ListarEditaisResult> Handle(
        ListarEditaisQuery query,
        IEditalRepository editalRepository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(editalRepository);

        (IReadOnlyList<Edital> itens, Guid? anteriorAfterId, Guid? proximoAfterId) = await editalRepository
            .ListarPaginadoAsync(query.AfterId, query.Limit, query.Direction, cancellationToken)
            .ConfigureAwait(false);

        EditalDto[] items = [.. itens.Select(Project)];
        return new ListarEditaisResult(items, anteriorAfterId, proximoAfterId);
    }

    private static EditalDto Project(Edital edital) => new(
        edital.Id,
        edital.NumeroEdital.ToString(),
        edital.Titulo,
        edital.TipoEditalId,
        edital.Status.ToString(),
        edital.MaximoOpcoesCurso,
        edital.BonusRegionalHabilitado,
        edital.CreatedAt);
}
