namespace Unifesspa.UniPlus.Autorizacao.Application.Avisos;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>Quem recebe o aviso por ter uma permissão: os titulares com e-mail na concessão.</summary>
internal static class TitularesDoAviso
{
    public static async Task<List<DestinatarioDoAviso>> ListarAsync(
        ITitularesDaPermissao titulares, string codigoDaPermissao, CancellationToken cancellationToken) =>
        [.. (await titulares.ListarTitularesComEmailAsync(codigoDaPermissao, cancellationToken).ConfigureAwait(false))
            .Select(static t => new DestinatarioDoAviso(new Identidade(t.Emissor, t.Sub), t.Email))];
}
