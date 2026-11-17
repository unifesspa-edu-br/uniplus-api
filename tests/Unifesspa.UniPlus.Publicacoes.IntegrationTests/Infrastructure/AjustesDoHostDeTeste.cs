namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Monolito.Persistencia;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Variações do host do monólito para provar o que depende do relógio, do fuso institucional ou do
/// acesso ao banco, sem alterar a fixture compartilhada pelas demais classes.
/// </summary>
internal static class AjustesDoHostDeTeste
{
    private const string SecaoDaConferenciaDoCatalogo = "Publicacoes:CatalogoDeTiposAto";

    /// <summary>22h30 em Belém de 15/06/2026, que em UTC já é o dia 16.</summary>
    internal static readonly DateTimeOffset VinteEDuasEMeiaEmBelem = InstanteEmBelem.Em(2026, 6, 15, 22, 30);

    internal static WebApplicationFactory<HostAssemblyMarker> ComRelogioEm(
        this WebApplicationFactory<HostAssemblyMarker> host, DateTimeOffset instante) =>
        host.WithWebHostBuilder(builder => builder.ConfigureTestServices(servicos =>
        {
            servicos.RemoveAll<TimeProvider>();
            servicos.AddSingleton<TimeProvider>(new RelogioFixo(instante));
        }));

    internal static WebApplicationFactory<HostAssemblyMarker> ComFusoNaoReconhecido(
        this WebApplicationFactory<HostAssemblyMarker> host) =>
        host.WithWebHostBuilder(builder => builder.ConfigureTestServices(servicos =>
        {
            servicos.RemoveAll<IResolvedorFusoInstitucional>();
            servicos.AddSingleton<IResolvedorFusoInstitucional>(new ResolvedorFusoIndisponivelDeTeste());
        }));

    /// <summary>A conferência do catálogo é opt-in por ambiente: este host a liga, apontando para o mesmo banco.</summary>
    internal static WebApplicationFactory<HostAssemblyMarker> ComConferenciaDoCatalogoLigada(
        this WebApplicationFactory<HostAssemblyMarker> host) =>
        host.WithWebHostBuilder(builder => builder.UseSetting($"{SecaoDaConferenciaDoCatalogo}:Conferir", "true"));

    /// <summary>Aponta o contexto de dados para uma porta em que não há banco, de modo que toda leitura lança.</summary>
    internal static WebApplicationFactory<HostAssemblyMarker> ComBancoInacessivel(
        this WebApplicationFactory<HostAssemblyMarker> host) =>
        host.WithWebHostBuilder(builder => builder.ConfigureTestServices(servicos =>
        {
            servicos.RemoveAll<UniPlusDbContext>();
            servicos.AddScoped<UniPlusDbContext>(_ => ContextoSemBanco());
        }));

    /// <summary>Um contexto cujo banco não existe: serve a quem só precisa do modelo e do tradutor de consultas.</summary>
    internal static MonolitoDbContext ContextoSemBanco() =>
        new(new DbContextOptionsBuilder<MonolitoDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=inexistente;Username=teste;Password=teste;Timeout=1")
            .UseSnakeCaseNamingConvention()
            .Options);
}
