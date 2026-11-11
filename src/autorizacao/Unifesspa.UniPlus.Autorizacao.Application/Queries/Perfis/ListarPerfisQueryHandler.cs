namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Perfis;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Handler da listagem de perfis. A mecânica de keyset vive no repositório.</summary>
public static class ListarPerfisQueryHandler
{
    public static async Task<Result<ListarPerfisResult>> Handle(
        ListarPerfisQuery query,
        IVerificadorDeAcesso verificador,
        IPerfilRepository repository,
        CatalogoDaAplicacao catalogo,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(catalogo);

        DomainError? recusa = await RecusaDaLeituraDoPerfil.ConferirAsync(verificador, null, cancellationToken).ConfigureAwait(false);
        if (recusa is not null)
        {
            return Result<ListarPerfisResult>.Failure(recusa);
        }

        (IReadOnlyList<Perfil> itens, Guid? anterior, Guid? proximo) = await repository
            .ListarPaginadoAsync(query.AfterId, query.Limit, query.Direction, cancellationToken)
            .ConfigureAwait(false);

        PerfilListadoDto[] dtos = [.. itens.Select(p => PerfilMapping.ToListadoDto(p, catalogo))];

        return Result<ListarPerfisResult>.Success(new ListarPerfisResult(dtos, anterior, proximo));
    }
}
