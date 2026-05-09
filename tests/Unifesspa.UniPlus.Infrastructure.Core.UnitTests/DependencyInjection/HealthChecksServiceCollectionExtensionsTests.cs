namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.DependencyInjection;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

public sealed class HealthChecksServiceCollectionExtensionsTests
{
    private static IConfiguration BuildConfig(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void AddUniPlusHealthChecks_ServicesNulo_LancaArgumentNullException()
    {
        IServiceCollection? services = null;
        Action acao = () => services!.AddUniPlusHealthChecks(BuildConfig([]), "PortalDb");
        acao.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddUniPlusHealthChecks_ConfigurationNulo_LancaArgumentNullException()
    {
        ServiceCollection services = new();
        Action acao = () => services.AddUniPlusHealthChecks(null!, "PortalDb");
        acao.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddUniPlusHealthChecks_ConnectionStringNameVazio_LancaArgumentException(string raw)
    {
        ServiceCollection services = new();
        Action acao = () => services.AddUniPlusHealthChecks(BuildConfig([]), raw);
        acao.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddUniPlusHealthChecks_NenhumaConfig_NaoRegistraNenhumCheck()
    {
        // Garante via AddOptions o registro mínimo do pipeline IOptions — necessário porque
        // AddHealthChecks() não materializa HealthCheckServiceOptions sem AddLogging() ou
        // contexto IHostBuilder. Em produção, o IHostBuilder cobre isso; nos testes unit,
        // adicionamos explicitamente.
        ServiceCollection services = new();
        services.AddOptions();
        services.AddUniPlusHealthChecks(BuildConfig([]), "PortalDb");

        using ServiceProvider sp = services.BuildServiceProvider();
        HealthCheckServiceOptions opts = sp.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;

        opts.Registrations.Select(r => r.Name).Should().BeEmpty();
    }

    [Fact]
    public void AddUniPlusHealthChecks_TodasAsConfigsPresentes_RegistraTodosOsChecks()
    {
        Dictionary<string, string?> config = new()
        {
            ["ConnectionStrings:PortalDb"] = "Host=pg;Port=5432;Database=x;Username=u;Password=p",
            ["Redis:ConnectionString"] = "redis:6379",
            ["Storage:Endpoint"] = "minio:9000",
            ["Kafka:BootstrapServers"] = "kafka:9092",
        };

        ServiceCollection services = new();
        services.AddOptions();
        services.AddUniPlusHealthChecks(BuildConfig(config), "PortalDb");

        using ServiceProvider sp = services.BuildServiceProvider();
        HealthCheckServiceOptions opts = sp.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;

        opts.Registrations.Select(r => r.Name).Should().BeEquivalentTo(["postgres", "redis", "minio", "kafka"]);
    }

    [Fact]
    public void AddUniPlusHealthChecks_CadaCheckTagueadoComReady()
    {
        Dictionary<string, string?> config = new()
        {
            ["ConnectionStrings:PortalDb"] = "Host=pg;Port=5432;Database=x;Username=u;Password=p",
            ["Redis:ConnectionString"] = "redis:6379",
            ["Storage:Endpoint"] = "minio:9000",
            ["Kafka:BootstrapServers"] = "kafka:9092",
        };

        ServiceCollection services = new();
        services.AddOptions();
        services.AddUniPlusHealthChecks(BuildConfig(config), "PortalDb");

        using ServiceProvider sp = services.BuildServiceProvider();
        HealthCheckServiceOptions opts = sp.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;

        // ReadyTag está em todos para que o readiness probe agregue corretamente.
        opts.Registrations.Should().AllSatisfy(r =>
            r.Tags.Should().Contain(HealthChecksServiceCollectionExtensions.ReadyTag));
    }

    [Fact]
    public void AddUniPlusHealthChecks_ApenasPostgres_RegistraSomentePostgres()
    {
        Dictionary<string, string?> config = new()
        {
            ["ConnectionStrings:SelecaoDb"] = "Host=pg;Port=5432;Database=x;Username=u;Password=p",
        };

        ServiceCollection services = new();
        services.AddUniPlusHealthChecks(BuildConfig(config), "SelecaoDb");

        using ServiceProvider sp = services.BuildServiceProvider();
        HealthCheckServiceOptions opts = sp.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;

        opts.Registrations.Select(r => r.Name).Should().BeEquivalentTo(["postgres"]);
    }
}
