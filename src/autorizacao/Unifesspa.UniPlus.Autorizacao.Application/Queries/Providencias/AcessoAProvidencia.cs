namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Providencias;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Application.Permissoes;

/// <summary>
/// O registro de cada providência exibida, antes de a resposta sair (ADR-0153). A borda já autorizou quem administra
/// concessões ou quem solicita providências; a permissão que autorizou é a que nomeia cada registro: administrar, se a
/// pessoa a tem, senão solicitar.
/// </summary>
internal static class AcessoAProvidencia
{
    private static readonly PermissionRequirement Administrar =
        CatalogoDePermissoes<PermissaoAutorizacao>.Obter(PermissaoAutorizacao.ConcessoesAdministrar).Requisito;

    private static readonly PermissionRequirement Solicitar =
        CatalogoDePermissoes<PermissaoAutorizacao>.Obter(PermissaoAutorizacao.ProvidenciasSolicitar).Requisito;

    public static async Task RegistrarLeituraAsync(
        IVerificadorDeAcesso verificador, IReadOnlyCollection<Guid> providenciaIds, CancellationToken cancellationToken)
    {
        if (providenciaIds.Count == 0)
        {
            return;
        }

        DecisaoDeAcesso leitura = await AvaliarAsync(verificador, Administrar, cancellationToken).ConfigureAwait(false);
        leitura = leitura is DecisaoPermitida ? leitura : await AvaliarAsync(verificador, Solicitar, cancellationToken).ConfigureAwait(false);
        if (leitura is DecisaoPermitida permitida)
        {
            await verificador.RegistrarParaCadaAsync(
                permitida, ResourceTiposAutorizacao.Providencia, providenciaIds, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    private static Task<DecisaoDeAcesso> AvaliarAsync(
        IVerificadorDeAcesso verificador, PermissionRequirement permissao, CancellationToken cancellationToken) =>
        verificador.AvaliarAsync(
            permissao, ResourceContext.From(ResourceTiposAutorizacao.Providencia, permissao.Sensibilidade).Value, cancellationToken);
}
