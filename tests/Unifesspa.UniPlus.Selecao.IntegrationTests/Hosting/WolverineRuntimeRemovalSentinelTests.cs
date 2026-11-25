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
        IHostBuilder hostBuilder = CriarHostComWolverine();

        ConferirServicosHospedadosDoWolverine(hostBuilder);
    }

    private static void ConferirServicosHospedadosDoWolverine(IHostBuilder hostBuilder)
    {
        IServiceCollection? capturedServices = null;
        hostBuilder.ConfigureServices((_, services) => capturedServices = services);
        using IHost host = hostBuilder.Build();
        capturedServices.Should().NotBeNull(
            "ConfigureServices é chamado durante Build pelo IHostBuilder; se ficou null, o pipeline mudou.");
        ServiceDescriptor[] hostedFromWolverine = ObterServicosHospedadosDoWolverine(capturedServices!);
        ConferirRemocaoDoWolverine(hostedFromWolverine);
    }

    private static void ConferirRemocaoDoWolverine(ServiceDescriptor[] hostedFromWolverine)
    {
        hostedFromWolverine.Should().NotBeEmpty(
            because: "ApiFactoryBase.DisableWolverineRuntimeForTests depende EXATAMENTE desta query "
            + "para remover WolverineRuntime; se Wolverine renomear o assembly, trocar "
            + "ImplementationFactory por ImplementationType, ou mover o registro para outro shape, "
            + "atualizar tests/Unifesspa.UniPlus.IntegrationTests.Fixtures/Hosting/ApiFactoryBase.cs "
            + "ANTES que suites com DisableWolverineRuntimeForTests=true falhem por timeout em MigrateAsync.");
    }

    private static IHostBuilder CriarHostComWolverine()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SentinelDb"] = "Host=sentinel-not-real;Database=fake;Username=u;Password=p",
                ["Kafka:BootstrapServers"] = string.Empty,
            })
            .Build();
        IHostBuilder hostBuilder = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(b => b.AddConfiguration(configuration));
        hostBuilder.UseWolverineOutboxCascading(configuration, connectionStringName: "SentinelDb");
        return hostBuilder;
    }

    private static ServiceDescriptor[] ObterServicosHospedadosDoWolverine(IServiceCollection services) =>
        [.. services.Where(d => d.ServiceType == typeof(IHostedService)
            && d.ImplementationFactory is not null
            && d.ImplementationFactory.Method.DeclaringType?.Assembly.GetName().Name == "Wolverine")];
}
