namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.ExternalServices;

using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;

/// <summary>
/// Os links da governança de perfis na Configuração: <c>{endereço}/acessos/perfis/{id}</c> e
/// <c>{endereço}/acessos/perfis/{id}/versoes/{versaoId}</c>.
/// </summary>
public sealed class LinkDaGovernanca : ILinkDaGovernanca
{
    private readonly string _base;

    public LinkDaGovernanca(IOptions<AvisosDoPedidoOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _base = options.Value.EnderecoDaConfiguracao.TrimEnd('/');
    }

    public string DoPerfil(Guid perfilId) => $"{_base}/acessos/perfis/{perfilId}";

    public string DaVersao(Guid perfilId, Guid versaoId) => $"{DoPerfil(perfilId)}/versoes/{versaoId}";
}
