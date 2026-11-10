namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Perfis;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;
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
        LeituraDaJanela janelas,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(janelas);
        ArgumentNullException.ThrowIfNull(userContext);

        Perfil? perfil = await repository.ObterPorIdAsync(query.Id, cancellationToken).ConfigureAwait(false);
        if (perfil is null)
        {
            return Result<PerfilDto>.Failure(PerfilHandlerErros.NaoEncontrado(query.Id));
        }

        JanelaDoPerfilDto? janela = await janelas.ResumirAsync(perfil.Id, cancellationToken).ConfigureAwait(false);

        return Result<PerfilDto>.Success(PerfilMapping.ToDto(perfil, catalogo, IdentidadeDoToken.De(userContext)) with { Janela = janela });
    }
}
