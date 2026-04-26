namespace Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
    /// <summary>
    /// Quando <c>true</c> (default), o factory remove o
    /// <see cref="WolverineRuntime"/> da lista de <see cref="IHostedService"/>
    /// — o host inicializa sem startar o Wolverine, evitando que testes que
    /// só exercitam o pipeline HTTP precisem de Postgres ou Kafka. Subclasses
    /// que exercitam a infra produtiva de outbox (PG queue, durable envelopes)
    /// sobrescrevem para <c>false</c> e provisionam o Postgres efêmero por
    /// fixture (ver <c>CascadingFixture</c>).
    /// </summary>
    protected virtual bool DisableWolverineRuntimeForTests => true;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(GetConfigurationOverrides());
        });

        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName,
                    _ => { });

            if (DisableWolverineRuntimeForTests)
            {
                // Wolverine registra o WolverineRuntime como IHostedService via
                // factory (ImplementationFactory != null, ImplementationType == null),
                // o que torna a inspeção por tipo concreto inviável. A heurística
                // estável é casar pelo assembly da fábrica: factories registradas
                // pelo próprio assembly Wolverine são removidas — o host inicializa
                // sem startar o runtime e, portanto, sem disparar MigrateAsync,
                // que tentaria conectar no Postgres configurado em
                // PersistMessagesWithPostgresql.
                ServiceDescriptor[] hostedToRemove = [.. services
                    .Where(d => d.ServiceType == typeof(IHostedService)
                        && d.ImplementationFactory is not null
                        && d.ImplementationFactory.Method.DeclaringType?.Assembly.GetName().Name == "Wolverine")];
                foreach (ServiceDescriptor svc in hostedToRemove)
                {
                    services.Remove(svc);
                }
            }
        });
    }

    /// <summary>
    /// Returns module-specific configuration overrides (e.g., connection strings, auth authority).
    /// </summary>
    protected abstract IEnumerable<KeyValuePair<string, string?>> GetConfigurationOverrides();
}
