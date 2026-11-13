namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.ExternalServices;

using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;

/// <summary>
/// Os links da governança de perfis na Configuração: <c>{endereço}/acessos/perfis/{id}</c> e
/// <c>{endereço}/acessos/perfis/{id}/versoes/{versaoId}</c>, <c>{endereço}/acessos/perfis/{id}/janela</c>,
/// <c>{endereço}/acessos/pedidos</c>, <c>{endereço}/acessos/pedidos/novo</c> e
/// <c>{endereço}/acessos/providencias/{id}</c>.
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

    public string DaJanela(Guid perfilId) => $"{DoPerfil(perfilId)}/janela";

    // Provisório: a tela das concessões exige administrá-las, e o beneficiário comum não a abre (uniplus-web#1457).
    public string DosPedidos() => $"{_base}/acessos/pedidos";

    public string DoNovoPedido() => $"{_base}/acessos/pedidos/novo";

    public string DaProvidencia(Guid providenciaId) => $"{_base}/acessos/providencias/{providenciaId}";
}
