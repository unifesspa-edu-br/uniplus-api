namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Perfis;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Handler da leitura de um perfil pelo Id.</summary>
public static class ObterPerfilQueryHandler
{
    public static async Task<Result<PerfilDto>> Handle(
        ObterPerfilQuery query,
        IPerfilRepository repository,
        CatalogoDaAplicacao catalogo,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(catalogo);

        Perfil? perfil = await repository.ObterPorIdAsync(query.Id, cancellationToken).ConfigureAwait(false);

        return perfil is null
            ? Result<PerfilDto>.Failure(PerfilHandlerErros.NaoEncontrado(query.Id))
            : Result<PerfilDto>.Success(PerfilMapping.ToDto(perfil, catalogo));
    }
}
