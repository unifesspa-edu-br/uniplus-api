namespace Unifesspa.UniPlus.Selecao.Application.Queries.ObrigatoriedadesLegais;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Mappings;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Handler convention-based de <see cref="ListarObrigatoriedadesLegaisQuery"/>.
/// Paginação keyset bidirecional por <c>Id</c> (ADR-0026 + ADR-0089 + Guid v7).
/// A mecânica de keyset (probe <c>n+1</c>, reversão, flags sem COUNT) vive no
/// repositório via <c>CursorKeyset</c>; o handler apenas projeta em DTO.
/// </summary>
public static class ListarObrigatoriedadesLegaisQueryHandler
{
    public static async Task<ListarObrigatoriedadesLegaisResult> Handle(
        ListarObrigatoriedadesLegaisQuery query,
        IObrigatoriedadeLegalRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        (IReadOnlyList<ObrigatoriedadeLegal> itens, Guid? anteriorAfterId, Guid? proximoAfterId) =
            await repository.ListarPaginadoAsync(
                query.AfterId,
                query.Limit,
                query.Direction,
                query.TipoProcessoCodigo,
                query.Categoria,
                query.Vigentes,
                cancellationToken).ConfigureAwait(false);

        ObrigatoriedadeLegalDto[] items =
            [.. itens.Select(ObrigatoriedadeLegalMapping.ToDto)];

        return new ListarObrigatoriedadesLegaisResult(items, anteriorAfterId, proximoAfterId);
    }
}
