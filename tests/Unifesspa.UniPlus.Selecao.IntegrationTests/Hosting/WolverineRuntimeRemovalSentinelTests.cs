namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Hosting;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Unifesspa.UniPlus.Infrastructure.Core.Messaging;

/// <summary>
/// Sentinela da heurística usada em
/// <c>ApiFactoryBase.ConfigureWebHost</c> (issue #194) para remover o
/// <c>WolverineRuntime</c> dos hosts de teste com
/// <c>DisableWolverineRuntimeForTests = true</c>.
///
/// A heurística é frágil contra refactors internos do JasperFx.Wolverine:
/// <code>
/// d.ServiceType == typeof(IHostedService)
///     &amp;&amp; d.ImplementationFactory is not null
///     &amp;&amp; d.ImplementationFactory.Method.DeclaringType?.Assembly.GetName().Name == "Wolverine"
/// </code>
///
/// Quando o Wolverine renomear o assembly (ex.: <c>JasperFx.Wolverine</c>) ou
/// migrar o registro do runtime para <c>ImplementationType</c> em vez de
/// factory, a remoção volta a ser silenciosamente no-op — o host inicializa
/// o runtime real, dispara <c>MigrateAsync</c> contra um Postgres de
/// referência inválido, e os testes falham por timeout, não pela causa raiz.
/// Este teste executa a mesma query LINQ que a heurística produtiva e
/// asserts que ela ainda casa — falha cedo com mensagem orientando a
/// atualização.
/// </summary>
public sealed class WolverineRuntimeRemovalSentinelTests
{
    [Fact(DisplayName = "Heurística de remoção do WolverineRuntime ainda casa após UseWolverineOutboxCascading")]
    public void Heuristica_Casa_AoMenosUm_IHostedService_DoAssemblyWolverine()
    {
        // UseWolverineOutboxCascading estende IHostBuilder (legacy host model),
        // não HostApplicationBuilder — usar Host.CreateDefaultBuilder.
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Connection string sintética — UseWolverineOutboxCascading registra
                // serviços, mas só conecta no banco quando o IHostedService inicia.
                // Build sem Run não dispara conexão.
                ["ConnectionStrings:SentinelDb"] =
                    "Host=sentinel-not-real;Database=fake;Username=u;Password=p",
                // Desliga Kafka — sem isto Wolverine tentaria iniciar transporte.
                ["Kafka:BootstrapServers"] = string.Empty,
            })
            .Build();

        IHostBuilder hostBuilder = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(b => b.AddConfiguration(configuration));

        hostBuilder.UseWolverineOutboxCascading(
            configuration,
            connectionStringName: "SentinelDb");

        // Captura o IServiceCollection efetivo antes do Build — necessário
        // porque a heurística produtiva inspeciona ServiceDescriptor (metadado
        // de registro, antes da instância existir), não o tipo da instância
        // resolvida. Cair no .GetServices<IHostedService>() depois do build
        // testaria proxy da heurística (assembly do tipo concreto), não a
        // heurística em si (assembly do método factory).
        IServiceCollection? capturedServices = null;
        hostBuilder.ConfigureServices((_, services) => capturedServices = services);

        using IHost host = hostBuilder.Build();

        capturedServices.Should().NotBeNull(
            "ConfigureServices é chamado durante Build pelo IHostBuilder; se ficou null, o pipeline mudou.");

        // Roda EXATAMENTE a query da heurística (espelho de
        // tests/Unifesspa.UniPlus.IntegrationTests.Fixtures/Hosting/ApiFactoryBase.cs).
        ServiceDescriptor[] hostedFromWolverine = [.. capturedServices!
            .Where(d => d.ServiceType == typeof(IHostedService)
                && d.ImplementationFactory is not null
                && d.ImplementationFactory.Method.DeclaringType?.Assembly.GetName().Name == "Wolverine")];

        hostedFromWolverine.Should().NotBeEmpty(
            because: "ApiFactoryBase.DisableWolverineRuntimeForTests depende EXATAMENTE desta query "
            + "para remover WolverineRuntime; se Wolverine renomear o assembly, trocar "
            + "ImplementationFactory por ImplementationType, ou mover o registro para outro shape, "
            + "atualizar tests/Unifesspa.UniPlus.IntegrationTests.Fixtures/Hosting/ApiFactoryBase.cs "
            + "ANTES que suites com DisableWolverineRuntimeForTests=true falhem por timeout em MigrateAsync.");
    }
}
