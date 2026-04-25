namespace Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// Base <see cref="WebApplicationFactory{TEntryPoint}"/> that wires up shared integration test
/// configuration: Development environment, test authentication scheme, and per-module config
/// overrides supplied by derived classes via <see cref="GetConfigurationOverrides"/>.
/// </summary>
/// <typeparam name="TEntryPoint">The program entry point of the API under test.</typeparam>
public abstract class ApiFactoryBase<TEntryPoint> : WebApplicationFactory<TEntryPoint>
    where TEntryPoint : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("Development");

        // UseSetting escreve no IConfigurationBuilder do host antes do Program.cs
        // do projeto-alvo executar — necessário porque o Program.cs lê a connection
        // string eagerly via builder.Configuration. ConfigureAppConfiguration roda
        // tarde demais (durante Build, depois de o Program.cs já ter capturado o
        // valor) para sobrescrever connection strings.
        foreach (KeyValuePair<string, string?> kv in GetConfigurationOverrides())
        {
            builder.UseSetting(kv.Key, kv.Value);
        }

        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName,
                    _ => { });
        });
    }

    /// <summary>
    /// Returns module-specific configuration overrides (e.g., connection strings, auth authority).
    /// </summary>
    protected abstract IEnumerable<KeyValuePair<string, string?>> GetConfigurationOverrides();
}
