namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Publicacoes.Application.Permissoes;

/// <summary>
/// API UniPlus com Wolverine e Postgres, em que o administrador anônimo dos testes tem a concessão
/// de todas as permissões do módulo Publicações.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "WebApplicationFactory<T> derivative usado como estado de fixture.")]
public sealed class PublicacoesEndpointFactory : MonolitoApiFactory
{
    public PublicacoesEndpointFactory(string connectionString)
        : base(connectionString, wolverineEnabled: true)
    {
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            services.AdicionarAdministradorInstitucionalDeTeste<PermissaoPublicacoes>();
            services.AdicionarRegistroDeDecisoesDeTeste();
        });
    }
}
