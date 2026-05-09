namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.DependencyInjection;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Caching;
using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

public sealed class CacheServiceCollectionExtensionsTests
{
    private static IConfiguration BuildConfig(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static HostingEnvironment Env(string name) =>
        new() { EnvironmentName = name };

    private static Dictionary<string, string?> CompleteConfig() => new()
    {
        ["Redis:ConnectionString"] = "redis:6379",
    };

    [Fact]
    public void AddUniPlusCache_ServicesNulo_LancaArgumentNullException()
    {
        IServiceCollection? services = null;

        Action acao = () => services!.AddUniPlusCache(BuildConfig(CompleteConfig()), Env(Environments.Production));

        acao.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddUniPlusCache_ConfigurationNulo_LancaArgumentNullException()
    {
        ServiceCollection services = new();

        Action acao = () => services.AddUniPlusCache(null!, Env(Environments.Production));

        acao.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddUniPlusCache_EnvironmentNulo_LancaArgumentNullException()
    {
        ServiceCollection services = new();

        Action acao = () => services.AddUniPlusCache(BuildConfig(CompleteConfig()), null!);

        acao.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddUniPlusCache_ProductionSemConnectionString_OptionsValueLancaOptionsValidationException()
    {
        ServiceCollection services = new();
        services.AddUniPlusCache(BuildConfig(new Dictionary<string, string?>()), Env(Environments.Production));

        using ServiceProvider sp = services.BuildServiceProvider();

        Action acao = () => { _ = sp.GetRequiredService<IOptions<RedisOptions>>().Value; };

        acao.Should().Throw<OptionsValidationException>()
            .WithMessage("*Redis:ConnectionString*");
    }

    [Fact]
    public void AddUniPlusCache_DevelopmentSemConfig_NaoLancaValidacao()
    {
        ServiceCollection services = new();
        services.AddUniPlusCache(BuildConfig(new Dictionary<string, string?>()), Env(Environments.Development));

        using ServiceProvider sp = services.BuildServiceProvider();

        Action acao = () => { _ = sp.GetRequiredService<IOptions<RedisOptions>>().Value; };

        acao.Should().NotThrow();
    }

    [Fact]
    public void AddUniPlusCache_BindingMapeiaConnectionString()
    {
        Dictionary<string, string?> values = new()
        {
            ["Redis:ConnectionString"] = "redis.example:6379,user=u,password=p",
        };

        ServiceCollection services = new();
        services.AddUniPlusCache(BuildConfig(values), Env(Environments.Production));

        using ServiceProvider sp = services.BuildServiceProvider();
        RedisOptions opts = sp.GetRequiredService<IOptions<RedisOptions>>().Value;

        opts.ConnectionString.Should().Be("redis.example:6379,user=u,password=p");
    }
}
