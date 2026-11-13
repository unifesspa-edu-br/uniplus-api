namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Providencias;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Application.Permissoes;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// A leitura das providências decidida e registrada item a item antes de a resposta sair (ADR-0153), na leitura das
/// providências e na trilha da concessão. A permissão que autorizou é a que nomeia cada registro: administrar, se a pessoa
/// a tem, senão solicitar.
/// </summary>
internal static class AcessoAProvidencia
{
    private static readonly PermissionRequirement Administrar =
        CatalogoDePermissoes<PermissaoAutorizacao>.Obter(PermissaoAutorizacao.ConcessoesAdministrar).Requisito;

    private static readonly PermissionRequirement Solicitar =
        CatalogoDePermissoes<PermissaoAutorizacao>.Obter(PermissaoAutorizacao.ProvidenciasSolicitar).Requisito;

    /// <summary>
    /// As providências que quem lê pode ver, cada uma registrada antes de a resposta sair: todas, se a decisão no instante
    /// da leitura permite, senão nenhuma, sem registro. É o ponto único da leitura das providências, no detalhe, na fila e
    /// na trilha da concessão: a permissão pode ter deixado de valer depois do filtro da borda, e o dado não sai sem ela. A
    /// falha do registro lança, e a resposta é 503.
    /// </summary>
    public static async Task<IReadOnlyList<T>> LiberarAsync<T>(
        IVerificadorDeAcesso verificador, IReadOnlyList<T> providencias, CancellationToken cancellationToken)
        where T : IIdentificavel
    {
        ArgumentNullException.ThrowIfNull(providencias);

        if (providencias.Count == 0 || await LeituraPermitidaAsync(verificador, cancellationToken).ConfigureAwait(false) is not { } permitida)
        {
            return [];
        }

        await verificador.RegistrarParaCadaAsync(
            permitida, ResourceTiposAutorizacao.Providencia, [.. providencias.Select(static p => p.Id)], cancellationToken: cancellationToken).ConfigureAwait(false);
        return providencias;
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
