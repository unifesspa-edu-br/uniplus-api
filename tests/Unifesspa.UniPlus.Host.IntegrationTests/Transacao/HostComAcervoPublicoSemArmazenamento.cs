namespace Unifesspa.UniPlus.Host.IntegrationTests.Transacao;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;

/// <summary>
/// Host cujo acervo público não tem armazenamento: a cópia dos documentos de um ato roda inteira, o handler real e o
/// banco real, e o teste conta o que foi copiado. Um por classe de teste, como os demais hosts desta pasta.
/// </summary>
public sealed class HostComAcervoPublicoSemArmazenamento : IDisposable
{
    private readonly Lock _trava = new();
    private WebApplicationFactory<HostAssemblyMarker>? _host;

    internal IServiceProvider Servicos(MonolitoPostgresFixture fixture)
    {
        lock (_trava)
        {
            _host ??= fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(static servicos =>
            {
                servicos.RemoveAll<IAcervoPublico>();
                servicos.AddSingleton<AcervoPublicoSemArmazenamento>();
                servicos.AddSingleton<IAcervoPublico>(static s => s.GetRequiredService<AcervoPublicoSemArmazenamento>());
            }));

            return _host.Services;
        }
    }

    public void Dispose()
    {
        _host?.Dispose();
    }
}
