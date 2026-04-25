namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Spike;

using Wolverine;

[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit IClassFixture<T> requires the fixture/factory type to be public.")]
public sealed class SelecaoOutboxApiFactory(string connectionString) : ApiFactoryBase<Program>
{
    private readonly string _connectionString = connectionString;

    protected override IEnumerable<KeyValuePair<string, string?>> GetConfigurationOverrides() =>
    [
        new("ConnectionStrings:SelecaoDb", _connectionString),
        new("Auth:Authority", "http://localhost/test-realm"),
        new("Auth:Audience", "uniplus"),
    ];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            // SPIKE V3: registra extension Wolverine que inclui o assembly de testes
            // na discovery (handler) e adiciona rota durável para EditalPublicadoEvent.
            services.AddSingleton<IWolverineExtension, SpikeWolverineExtension>();
        });
    }
}
