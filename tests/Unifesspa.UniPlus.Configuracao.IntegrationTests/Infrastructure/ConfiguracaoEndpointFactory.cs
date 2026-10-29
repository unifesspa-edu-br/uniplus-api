namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;

using Unifesspa.UniPlus.Configuracao.Application.Permissoes;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// API UniPlus com Wolverine e Postgres, em que o administrador anônimo dos testes tem a concessão
/// de todas as permissões do módulo Configuração.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "WebApplicationFactory<T> derivative usado como estado de fixture.")]
public sealed class ConfiguracaoEndpointFactory : MonolitoApiFactory
{
    public ConfiguracaoEndpointFactory(string connectionString)
        : base(connectionString, wolverineEnabled: true)
    {
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            services.AdicionarAdministradorInstitucionalDeTeste<PermissaoConfiguracao>();
            services.AdicionarRegistroDeDecisoesDeTeste();
        });
    }
}
