namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Perfis;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

/// <summary>Handler da listagem de perfis. A mecânica de keyset vive no repositório.</summary>
public static class ListarPerfisQueryHandler
{
    public static async Task<ListarPerfisResult> Handle(
        ListarPerfisQuery query,
        IPerfilRepository repository,
        CatalogoDaAplicacao catalogo,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(catalogo);

        (IReadOnlyList<Perfil> itens, Guid? anterior, Guid? proximo) = await repository
            .ListarPaginadoAsync(query.AfterId, query.Limit, query.Direction, cancellationToken)
            .ConfigureAwait(false);

        PerfilDto[] dtos = [.. itens.Select(p => PerfilMapping.ToDto(p, catalogo))];

        return new ListarPerfisResult(dtos, anterior, proximo);
    }
}
