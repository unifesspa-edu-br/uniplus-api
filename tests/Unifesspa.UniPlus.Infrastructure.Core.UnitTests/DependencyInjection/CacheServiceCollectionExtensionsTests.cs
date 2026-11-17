namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.DependencyInjection;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

using Unifesspa.UniPlus.Infrastructure.Core.Caching;
using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

public sealed class CacheServiceCollectionExtensionsTests
{
    private static IConfiguration CriarConfiguracao(Dictionary<string, string?> valores) =>
        new ConfigurationBuilder().AddInMemoryCollection(valores).Build();

    private static HostingEnvironment CriarAmbiente(string nome) =>
        new() { EnvironmentName = nome };

    private static Dictionary<string, string?> ConfiguracaoCompleta() => new()
    {
        ["Redis:ConnectionString"] = "redis:6379",
    };

    [Fact]
    public void AddUniPlusCache_ServicesNulo_LancaArgumentNullException()
    {
        IServiceCollection? services = null;

        Action acao = () => services!.AddUniPlusCache(CriarConfiguracao(ConfiguracaoCompleta()), CriarAmbiente(Environments.Production));

        acao.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddUniPlusCache_ConfigurationNulo_LancaArgumentNullException()
    {
        ServiceCollection services = new();

        Action acao = () => services.AddUniPlusCache(null!, CriarAmbiente(Environments.Production));

        acao.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddUniPlusCache_EnvironmentNulo_LancaArgumentNullException()
    {
        ServiceCollection services = new();

        Action acao = () => services.AddUniPlusCache(CriarConfiguracao(ConfiguracaoCompleta()), null!);

        acao.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddUniPlusCache_ProductionSemConnectionString_OptionsValueLancaOptionsValidationException()
    {
        ServiceCollection services = new();
        services.AddUniPlusCache(CriarConfiguracao(new Dictionary<string, string?>()), CriarAmbiente(Environments.Production));

        using ServiceProvider provedor = services.BuildServiceProvider();

        Action acao = () => { _ = provedor.GetRequiredService<IOptions<RedisOptions>>().Value; };

        acao.Should().Throw<OptionsValidationException>()
            .WithMessage("*Redis:ConnectionString*");
    }

    [Fact]
    public void AddUniPlusCache_DevelopmentSemConfig_NaoLancaValidacao()
    {
        ServiceCollection services = new();
        services.AddUniPlusCache(CriarConfiguracao(new Dictionary<string, string?>()), CriarAmbiente(Environments.Development));

        using ServiceProvider provedor = services.BuildServiceProvider();

        Action acao = () => { _ = provedor.GetRequiredService<IOptions<RedisOptions>>().Value; };

        acao.Should().NotThrow();
    }

    [Fact]
    public void AddUniPlusCache_BindingMapeiaConnectionString()
    {
        Dictionary<string, string?> valores = new()
        {
            ["Redis:ConnectionString"] = "redis.example:6379,user=u,password=p",
        };

        ServiceCollection services = new();
        services.AddUniPlusCache(CriarConfiguracao(valores), CriarAmbiente(Environments.Production));

        using ServiceProvider provedor = services.BuildServiceProvider();
        RedisOptions opcoes = provedor.GetRequiredService<IOptions<RedisOptions>>().Value;

        opcoes.ConnectionString.Should().Be("redis.example:6379,user=u,password=p");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddUniPlusCache_ProductionComConnectionStringEmBranco_OptionsValueLancaOptionsValidationException(string valor)
    {
        ServiceCollection services = new();
        services.AddUniPlusCache(
            CriarConfiguracao(new Dictionary<string, string?> { ["Redis:ConnectionString"] = valor }),
            CriarAmbiente(Environments.Production));

        using ServiceProvider provedor = services.BuildServiceProvider();

        Action acao = () => { _ = provedor.GetRequiredService<IOptions<RedisOptions>>().Value; };

        acao.Should().Throw<OptionsValidationException>()
            .WithMessage("*Redis:ConnectionString must be configured outside Development.*");
    }

    [Fact]
    public void AddUniPlusCache_RegistraConexaoComoSingletonECacheComoScoped()
    {
        ServiceCollection services = new();
        services.AddUniPlusCache(CriarConfiguracao(ConfiguracaoCompleta()), CriarAmbiente(Environments.Production));

        services.Single(descritor => descritor.ServiceType == typeof(IConnectionMultiplexer))
            .Lifetime.Should().Be(ServiceLifetime.Singleton);
        ServiceDescriptor cache = services.Single(descritor => descritor.ServiceType == typeof(ICacheService));
        cache.Lifetime.Should().Be(ServiceLifetime.Scoped);
        cache.ImplementationType.Should().Be<RedisCacheService>();
    }
}
