namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;

/// <summary>
/// Host da instância anterior à troca de catálogo, um por classe de teste: durante a implantação ela segue atendendo
/// enquanto a nova conciliação grava o catálogo novo. Um host por teste acumulava memória, e mantê-lo vivo depois da
/// classe faria o consumidor dele disputar com o host da fixture as mensagens das demais classes.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige que a fixture de classe seja pública.")]
public sealed class HostComCatalogoAnterior : IDisposable
{
    private readonly Lock _trava = new();
    private WebApplicationFactory<HostAssemblyMarker>? _host;

    internal WebApplicationFactory<HostAssemblyMarker> Obter(MonolitoDaInstalacaoFixture fixture, CatalogoDaAplicacao anterior)
    {
        lock (_trava)
        {
            return _host ??= fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(servicos =>
            {
                servicos.RemoveAll<CatalogoDaAplicacao>();
                servicos.AddSingleton(anterior);
            }));
        }
    }

    public void Dispose()
    {
        _host?.Dispose();
    }
}
