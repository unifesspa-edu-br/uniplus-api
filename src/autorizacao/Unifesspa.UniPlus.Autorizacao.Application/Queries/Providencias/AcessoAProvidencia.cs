namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Providencias;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Application.Permissoes;

/// <summary>
/// O registro de cada providência exibida, antes de a resposta sair (ADR-0153), na leitura das providências e na trilha
/// da concessão. A permissão que autorizou é a que nomeia cada registro: administrar, se a
/// pessoa a tem, senão solicitar.
/// </summary>
internal static class AcessoAProvidencia
{
    private static readonly PermissionRequirement Administrar =
        CatalogoDePermissoes<PermissaoAutorizacao>.Obter(PermissaoAutorizacao.ConcessoesAdministrar).Requisito;

    private static readonly PermissionRequirement Solicitar =
        CatalogoDePermissoes<PermissaoAutorizacao>.Obter(PermissaoAutorizacao.ProvidenciasSolicitar).Requisito;

    /// <summary>
    /// Registra cada providência a exibir pela permissão que dá a leitura delas. Devolve se quem lê pode vê-las: falso
    /// quando nenhuma das duas permissões o autoriza, e nada é registrado. A falha do registro lança, e a resposta é 503.
    /// </summary>
    public static async Task<bool> RegistrarLeituraAsync(
        IVerificadorDeAcesso verificador, IReadOnlyCollection<Guid> providenciaIds, CancellationToken cancellationToken)
    {
        if (providenciaIds.Count == 0)
        {
            return true;
        }

        if (await LeituraPermitidaAsync(verificador, cancellationToken).ConfigureAwait(false) is not { } permitida)
        {
            return false;
        }

        await verificador.RegistrarParaCadaAsync(
            permitida, ResourceTiposAutorizacao.Providencia, providenciaIds, cancellationToken: cancellationToken).ConfigureAwait(false);
        return true;
    }

    // Administrar, se a pessoa a tem, senão solicitar; nula quando nenhuma das duas autoriza.
    private static async Task<DecisaoPermitida?> LeituraPermitidaAsync(IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        DecisaoDeAcesso leitura = await AvaliarAsync(verificador, Administrar, cancellationToken).ConfigureAwait(false);

        return leitura as DecisaoPermitida
            ?? await AvaliarAsync(verificador, Solicitar, cancellationToken).ConfigureAwait(false) as DecisaoPermitida;
    }

    private static Task<DecisaoDeAcesso> AvaliarAsync(
        IVerificadorDeAcesso verificador, PermissionRequirement permissao, CancellationToken cancellationToken) =>
        verificador.AvaliarAsync(
            permissao, ResourceContext.From(ResourceTiposAutorizacao.Providencia, permissao.Sensibilidade).Value, cancellationToken);
}
