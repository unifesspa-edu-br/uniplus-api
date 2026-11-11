namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Perfis;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler da leitura da janela de migração: quem mantém perfis ou aprova por alguma das etapas lê a janela mais recente
/// do perfil, encerrada ou não, com cada concessão na situação em que está.
/// </summary>
public static class ObterJanelaDoPerfilQueryHandler
{
    public static async Task<Result<JanelaDeMigracaoDto>> Handle(
        ObterJanelaDoPerfilQuery query, IVerificadorDeAcesso verificador, IPerfilRepository perfis,
        LeituraDaJanela leitura, IUserContext userContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(perfis);
        ArgumentNullException.ThrowIfNull(leitura);
        ArgumentNullException.ThrowIfNull(userContext);

        Result<Perfil> perfil = await PerfilLegivelAsync(query, verificador, perfis, cancellationToken).ConfigureAwait(false);

        return perfil.IsSuccess
            ? await LerAsync(perfil.Value!, leitura, IdentidadeDoToken.De(userContext), cancellationToken).ConfigureAwait(false)
            : Result<JanelaDeMigracaoDto>.Failure(perfil.Error!);
    }

    /// <summary>O perfil pedido, quando a pessoa pode lê-lo e ele existe; a recusa, quando não.</summary>
    private static async Task<Result<Perfil>> PerfilLegivelAsync(
        ObterJanelaDoPerfilQuery query, IVerificadorDeAcesso verificador, IPerfilRepository perfis, CancellationToken cancellationToken)
    {
        AcessoAvaliado acesso = await AcessoDaVersao.AvaliarLeituraDoPerfilAsync(verificador, query.PerfilId, cancellationToken).ConfigureAwait(false);
        if (acesso.Recusa(PerfilErros.SemPermissaoNaEtapa) is { } recusa)
        {
            return Result<Perfil>.Failure(recusa);
        }

        return await perfis.ObterPorIdAsync(query.PerfilId, cancellationToken).ConfigureAwait(false) is { } perfil
            ? Result<Perfil>.Success(perfil)
            : Result<Perfil>.Failure(PerfilHandlerErros.NaoEncontrado(query.PerfilId));
    }

    private static async Task<Result<JanelaDeMigracaoDto>> LerAsync(
        Perfil perfil, LeituraDaJanela leitura, Identidade? leitor, CancellationToken cancellationToken)
    {
        JanelaDeMigracaoDto? janela = await leitura.ObterAsync(perfil, leitor, cancellationToken).ConfigureAwait(false);

        return janela is null
            ? Result<JanelaDeMigracaoDto>.Failure(MigracaoErros.JanelaNaoEncontrada())
            : Result<JanelaDeMigracaoDto>.Success(janela);
    }
}
