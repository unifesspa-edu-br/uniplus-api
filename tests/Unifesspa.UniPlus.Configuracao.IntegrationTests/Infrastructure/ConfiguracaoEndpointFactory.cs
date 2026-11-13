namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Application.Permissoes;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// API UniPlus com Wolverine e Postgres, em que o administrador anônimo dos testes tem a concessão
/// de todas as permissões do módulo Configuração, e o relógio é o <see cref="RelogioDeTeste"/>, que o teste congela
/// quando precisa de escritas no mesmo instante.
/// </summary>
public sealed class ConfiguracaoEndpointFactory : MonolitoApiFactory
{
    internal RelogioDeTeste Relogio { get; } = new();

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

            // Instância, e não fábrica: o codegen do Wolverine resolve singleton registrado por instância sem
            // localização de serviço.
            services.AddSingleton(Relogio);
            services.AddSingleton<TimeProvider>(Relogio);
        });
    }
}
