namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Observability;

using System.Collections.Generic;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
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

    [Fact]
    public void SelecionarSampler_EmDevelopment_DeveRetornarAlwaysOnSampler()
    {
        IHostEnvironment environment = NovoAmbiente("Development");

        Sampler sampler = OpenTelemetryConfiguration.SelecionarSampler(environment);

        sampler.Should().BeOfType<AlwaysOnSampler>();
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("HML")]
    [InlineData("Test")]
    public void SelecionarSampler_ForaDeDevelopment_DeveRetornarParentBasedComTraceIdRatio10Pct(string environmentName)
    {
        IHostEnvironment environment = NovoAmbiente(environmentName);

        Sampler sampler = OpenTelemetryConfiguration.SelecionarSampler(environment);

        // ParentBasedSampler.Description segue o formato canônico OTel:
        // "ParentBased{root=TraceIdRatioBased{0.100000},...}". Validar pela
        // descrição evita reflection no campo privado _rootSampler e quebra de
        // teste em upgrades minor da SDK que reorganizem internals.
        sampler.Should().BeOfType<ParentBasedSampler>();
        sampler.Description.Should().Contain("TraceIdRatioBased").And.Contain("0.1");
    }

    [Fact]
    public void SelecionarSampler_EnvironmentNulo_DeveLancarArgumentNullException()
    {
        IHostEnvironment? environment = null;

        Action acao = () => OpenTelemetryConfiguration.SelecionarSampler(environment!);

        acao.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    [InlineData("/health/startup")]
    public void EhRotaInstrumentavel_RotasHealth_RetornaFalse(string path)
    {
        bool resultado = OpenTelemetryConfiguration.EhRotaInstrumentavel(new PathString(path));

        resultado.Should().BeFalse(because: $"a rota '{path}' é health check e não deve gerar telemetria");
    }

    [Theory]
    [InlineData("/api/editais")]
    [InlineData("/api/editais/123")]
    [InlineData("/api/inscricoes")]
    [InlineData("/swagger")]
    [InlineData("/healthz")]
    [InlineData("/")]
    public void EhRotaInstrumentavel_RotasNegocioEOutras_RetornaTrue(string path)
    {
        bool resultado = OpenTelemetryConfiguration.EhRotaInstrumentavel(new PathString(path));

        resultado.Should().BeTrue(because: $"a rota '{path}' deve ser instrumentada normalmente");
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
