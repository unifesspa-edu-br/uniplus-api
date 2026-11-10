namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;

using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;

/// <summary>
/// Host que provoca o conflito de concorrência na gravação, um por classe de teste: subir um por teste acumulava memória,
/// e mantê-lo vivo depois da classe faria o consumidor dele disputar com o host da fixture as mensagens das demais classes.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige que a fixture de classe seja pública.")]
public sealed class HostComConflitoNaGravacao : IDisposable
{
    private readonly Lock _trava = new();
    private WebApplicationFactory<HostAssemblyMarker>? _host;

    internal ConflitoNaGravacaoDoHost Conflito { get; } = new();

    internal WebApplicationFactory<HostAssemblyMarker> Obter(MonolitoDaInstalacaoFixture fixture)
    {
        lock (_trava)
        {
            return _host ??= fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(Conflito.Instalar));
        }
    }

    public void Dispose()
    {
        _host?.Dispose();
    }
}
