namespace Unifesspa.UniPlus.Selecao.Application.Queries.ObrigatoriedadesLegais;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain;
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
        ITipoProcessoReader tipos,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(tipos);

        (IReadOnlyList<ObrigatoriedadeLegal> itens, Guid? anteriorAfterId, Guid? proximoAfterId) =
            await repository.ListarPaginadoAsync(
                query.AfterId, query.Limit, query.Direction,
                query.TipoProcessoCodigo, query.Categoria, query.Vigentes,
                cancellationToken).ConfigureAwait(false);
        string[] codigos = [.. itens.Select(item => item.TipoProcessoCodigo)
            .Where(codigo => codigo != TodosOsTiposDeProcesso.Codigo).Distinct()];
        IReadOnlyDictionary<string, string> nomes = await tipos.ObterNomesPorCodigosAsync(codigos, cancellationToken).ConfigureAwait(false);
        return new ListarObrigatoriedadesLegaisResult(
            [.. itens.Select(item => ObrigatoriedadeLegalMapping.ToDto(item, nomes))], anteriorAfterId, proximoAfterId);
    }
}
