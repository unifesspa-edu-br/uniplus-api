namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Observability;

using System.Collections.Generic;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using NSubstitute;

using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

using Unifesspa.UniPlus.Infrastructure.Core.Observability;

public class OpenTelemetryConfigurationTests
{
    [Fact]
    public void AdicionarObservabilidade_QuandoToggleDesabilitado_NaoRegistraTracerProviderNemMeterProvider()
    {
        ServiceCollection services = new();
        IConfiguration configuration = NovaConfiguracao(new Dictionary<string, string?>
        {
            [OpenTelemetryConfiguration.EnabledConfigurationKey] = "false",
        });
        IHostEnvironment environment = NovoAmbiente("Development");

        services.AdicionarObservabilidade("uniplus-test", configuration, environment);

        ServiceProvider provider = services.BuildServiceProvider();
        provider.GetService<TracerProvider>().Should().BeNull();
        provider.GetService<MeterProvider>().Should().BeNull();
    }

    [Fact]
    public void AdicionarObservabilidade_QuandoToggleAtivo_RegistraTracerProviderEMeterProvider()
    {
        ServiceCollection services = new();
        IConfiguration configuration = NovaConfiguracao();
        IHostEnvironment environment = NovoAmbiente("Development");

        services.AdicionarObservabilidade("uniplus-test", configuration, environment);

        ServiceProvider provider = services.BuildServiceProvider();
        provider.GetService<TracerProvider>().Should().NotBeNull();
        provider.GetService<MeterProvider>().Should().NotBeNull();
    }

    [Fact]
    public void AdicionarObservabilidade_QuandoToggleAusente_AssumeAtivoPorDefault()
    {
        // Default seguro pra produção: ausência da chave significa "ligado".
        // CI explicitamente desliga via configuração quando não há Collector.
        ServiceCollection services = new();
        IConfiguration configuration = NovaConfiguracao();
        IHostEnvironment environment = NovoAmbiente("Production");

        services.AdicionarObservabilidade("uniplus-test", configuration, environment);

        ServiceProvider provider = services.BuildServiceProvider();
        provider.GetService<TracerProvider>().Should().NotBeNull();
    }

    [Fact]
    public void AdicionarObservabilidade_NomeServicoVazio_DeveLancarArgumentException()
    {
        ServiceCollection services = new();
        IConfiguration configuration = NovaConfiguracao();
        IHostEnvironment environment = NovoAmbiente("Development");

        Action acao = () => services.AdicionarObservabilidade(string.Empty, configuration, environment);

        acao.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AdicionarObservabilidade_ServicesNulo_DeveLancarArgumentNullException()
    {
        IServiceCollection? services = null;
        IConfiguration configuration = NovaConfiguracao();
        IHostEnvironment environment = NovoAmbiente("Development");

        Action acao = () => services!.AdicionarObservabilidade("uniplus-test", configuration, environment);

        acao.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AdicionarObservabilidade_ConfigurationNulo_DeveLancarArgumentNullException()
    {
        ServiceCollection services = new();
        IConfiguration? configuration = null;
        IHostEnvironment environment = NovoAmbiente("Development");

        Action acao = () => services.AdicionarObservabilidade("uniplus-test", configuration!, environment);

        acao.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AdicionarObservabilidade_EnvironmentNulo_DeveLancarArgumentNullException()
    {
        ServiceCollection services = new();
        IConfiguration configuration = NovaConfiguracao();
        IHostEnvironment? environment = null;

        Action acao = () => services.AdicionarObservabilidade("uniplus-test", configuration, environment!);

        acao.Should().Throw<ArgumentNullException>();
    }

    private static IConfiguration NovaConfiguracao(IEnumerable<KeyValuePair<string, string?>>? values = null)
    {
        ConfigurationBuilder builder = new();
        if (values is not null)
        {
            builder.AddInMemoryCollection(values);
        }

        return builder.Build();
    }

    private static IHostEnvironment NovoAmbiente(string environmentName)
    {
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);
        return environment;
    }
}
