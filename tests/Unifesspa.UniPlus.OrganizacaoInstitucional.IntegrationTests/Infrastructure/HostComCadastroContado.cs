namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Infrastructure;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Repositories;

/// <summary>
/// O host do teste com os repositórios de Unidade e de Instituição contando as chamadas recebidas.
/// </summary>
internal static class HostComCadastroContado
{
    /// <summary>Envia a requisição a um host cujos repositórios contam as chamadas na <paramref name="contagem"/>.</summary>
    public static async Task<HttpResponseMessage> EnviarComCadastroContadoAsync(
        this OrganizacaoEndpointFixture fixture, HttpRequestMessage requisicao, ConsultasAoCadastroContadas contagem)
    {
        await using WebApplicationFactory<HostAssemblyMarker> host = ComCadastroContado(fixture, contagem);
        using HttpClient client = host.CreateClient();
        return await client.SendAsync(requisicao);
    }

    /// <remarks>
    /// Os repositórios reais são registrados por tipo, como no host: o codegen do Wolverine monta o handler sem
    /// service location (ADR-0098), o que uma fábrica lambda impediria.
    /// </remarks>
    private static WebApplicationFactory<HostAssemblyMarker> ComCadastroContado(
        OrganizacaoEndpointFixture fixture, ConsultasAoCadastroContadas contagem) =>
        fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IUnidadeRepository>();
            services.RemoveAll<IInstituicaoRepository>();
            services.AddScoped<UnidadeRepository>();
            services.AddScoped<InstituicaoRepository>();
            services.AddSingleton(contagem);
            services.AddScoped<IUnidadeRepository, UnidadeRepositorioContado>();
            services.AddScoped<IInstituicaoRepository, InstituicaoRepositorioContado>();
        }));
}
