namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Perfis;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.RevisoesDasConcessoes;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>O detalhe de um perfil, com o resumo da revisão das concessões, montado para quem já pode lê-lo.</summary>
public sealed class DetalheDoPerfil
{
    private readonly IPerfilRepository _perfis;
    private readonly CatalogoDaAplicacao _catalogo;
    private readonly LeituraDaRevisaoDasConcessoes _revisoes;
    private readonly IUserContext _userContext;

    public DetalheDoPerfil(IPerfilRepository perfis, CatalogoDaAplicacao catalogo, LeituraDaRevisaoDasConcessoes revisoes, IUserContext userContext)
    {
        ArgumentNullException.ThrowIfNull(perfis);
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(revisoes);
        ArgumentNullException.ThrowIfNull(userContext);

        _perfis = perfis;
        _catalogo = catalogo;
        _revisoes = revisoes;
        _userContext = userContext;
    }

    public async Task<Result<PerfilDto>> MontarAsync(Guid perfilId, CancellationToken cancellationToken)
    {
        if (await _perfis.ObterPorIdAsync(perfilId, cancellationToken).ConfigureAwait(false) is not { } perfil)
        {
            return Result<PerfilDto>.Failure(PerfilHandlerErros.NaoEncontrado(perfilId));
        }

        RevisaoDoPerfilDto? revisao = await _revisoes.ResumirAsync(perfil.Id, cancellationToken).ConfigureAwait(false);

        return Result<PerfilDto>.Success(PerfilMapping.ToDto(perfil, _catalogo, IdentidadeDoToken.De(_userContext)) with { RevisaoDasConcessoes = revisao });
    }
}
