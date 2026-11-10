namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;

/// <summary>
/// Host com o relógio controlável, um por classe de teste: subir um por teste acumulava memória, e mantê-lo vivo depois
/// da classe faria o consumidor dele disputar com o host da fixture as mensagens das demais classes.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige que a fixture de classe seja pública.")]
public sealed class HostDaJanelaDeMigracao : IDisposable
{
    private readonly Lock _trava = new();
    private WebApplicationFactory<HostAssemblyMarker>? _host;

    internal RelogioAvancavelDeTeste Relogio { get; } = new();

    internal WebApplicationFactory<HostAssemblyMarker> Obter(MonolitoDaInstalacaoFixture fixture)
    {
        lock (_trava)
        {
            return _host ??= fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(servicos =>
            {
                servicos.RemoveAll<TimeProvider>();
                servicos.AddSingleton<TimeProvider>(Relogio);
            }));
        }
    }

    public void Dispose()
    {
        _host?.Dispose();
    }
}
