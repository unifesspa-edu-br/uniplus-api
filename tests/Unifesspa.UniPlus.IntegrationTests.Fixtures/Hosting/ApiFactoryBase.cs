namespace Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Authentication;

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
    /// só exercitam o pipeline HTTP precisem de Postgres ou Kafka.
    /// </summary>
    /// <remarks>
    /// <para><strong>Por que remover por default?</strong> A maioria das suítes
    /// de integração testa o pipeline HTTP (auth, routing, controllers,
    /// middleware) sem exercitar mensageria. Iniciar o Wolverine dispararia
    /// <c>MigrateAsync</c> contra o Postgres configurado em
    /// <c>PersistMessagesWithPostgresql</c> — em ambiente de teste sem PG
    /// real, isso vira timeout de 30+ segundos por suite, mascarando o erro
    /// real (não há PG) atrás de mensagens genéricas de connection.</para>
    ///
    /// <para><strong>Quando sobrescrever para <c>false</c>?</strong> Suites
    /// que exercitam a infra produtiva de outbox (PG queue durável,
    /// envelopes persistidos, cascading messages) precisam do Wolverine
    /// rodando. Essas suites provisionam um Postgres efêmero por fixture
    /// (ver <c>CascadingFixture</c> em <c>Selecao.IntegrationTests/Outbox/Cascading/</c>)
    /// e setam <c>DisableWolverineRuntimeForTests = false</c> na sua
    /// <see cref="ApiFactoryBase{T}"/>-derivada. <c>CascadingApiFactory</c>
    /// é o exemplo canônico.</para>
    ///
    /// <para><strong>Heurística de remoção e sentinela:</strong> a query no
    /// método <see cref="ConfigureWebHost"/> usa
    /// <c>d.ImplementationFactory.Method.DeclaringType?.Assembly.GetName().Name == "Wolverine"</c>
    /// para identificar o <see cref="IHostedService"/> que o Wolverine registra
    /// via factory delegate. Refactors internos do JasperFx.Wolverine (renomear
    /// assembly, migrar para <c>ImplementationType</c>) silenciosamente quebrariam
    /// este filtro, fazendo o runtime iniciar e timeoutar contra Postgres fake.
    /// O sentinela <c>WolverineRuntimeRemovalSentinelTests</c> em
    /// <c>Selecao.IntegrationTests/Hosting/</c> (issue #194) replica EXATAMENTE
    /// esta query e falha cedo se a heurística regredir.</para>
    /// </remarks>
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
            ConfigureTestAuthentication(services);

            if (DisableWolverineRuntimeForTests)
            {
                // Dois IHostedService produtivos precisam ser removidos juntos quando o
                // Postgres não está disponível no test host (default da maioria das suites
                // de integração que só exercitam o pipeline HTTP):
                //
                // 1. Wolverine registra o WolverineRuntime como IHostedService via factory
                //    (ImplementationFactory != null, ImplementationType == null). A heurística
                //    estável é casar pelo assembly da fábrica.
                // 2. MigrationHostedService<TContext> (issue #344) é registrado por
                //    AddDbContextMigrationsOnStartup<T>() e dispara MigrateAsync no StartAsync.
                //    Sem PG real, falha com "Connection refused" e derruba o factory antes do
                //    primeiro teste. Reconhecemos pelo ImplementationType genérico.
                //
                // Suites que exercitam outbox real (CascadingFixture) sobrescrevem
                // DisableWolverineRuntimeForTests=false e provisionam o PG efêmero — nesse
                // caso AMBOS os hosted services rodam contra o banco da fixture.
                ServiceDescriptor[] hostedToRemove = [.. services
                    .Where(d => d.ServiceType == typeof(IHostedService)
                        && (
                            (d.ImplementationFactory is not null
                                && d.ImplementationFactory.Method.DeclaringType?.Assembly.GetName().Name == "Wolverine")
                            || IsMigrationHostedService(d.ImplementationType)))];
                foreach (ServiceDescriptor svc in hostedToRemove)
                {
                    services.Remove(svc);
                }
            }
        });
    }

    private static bool IsMigrationHostedService(Type? implementationType) =>
        implementationType is { IsGenericType: true }
        && string.Equals(
            implementationType.GetGenericTypeDefinition().FullName,
            "Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection.MigrationHostedService`1",
            StringComparison.Ordinal);

    /// <summary>
    /// Substitui o esquema de autenticação produtivo pelo <see cref="TestAuthHandler"/>, permitindo
    /// que testes injetem identidades via headers HTTP sem emitir JWTs reais — esta é a configuração
    /// padrão da maioria das suítes de integração, que não precisam exercitar a validação criptográfica
    /// do JWT em si.
    ///
    /// Subclasses que exercitam o pipeline real <c>JwtBearer</c> (validação de issuer/audience/lifetime/
    /// signing key) contra um IdP real — p.ex. Keycloak via Testcontainers — sobrescrevem este método
    /// como no-op para PRESERVAR o esquema produtivo registrado pela API.
    /// </summary>
    /// <param name="services">A coleção de serviços do host de teste.</param>
    protected virtual void ConfigureTestAuthentication(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName,
                _ => { });
    }

    /// <summary>
    /// Returns module-specific configuration overrides (e.g., connection strings, auth authority).
    /// </summary>
    protected abstract IEnumerable<KeyValuePair<string, string?>> GetConfigurationOverrides();
}
