namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Authentication;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using NSubstitute;

using Unifesspa.UniPlus.Infrastructure.Core.Authentication;

public sealed class OidcAuthenticationConfigurationTests
{
    [Fact]
    public void AddOidcAuthentication_Should_RejectMissingAuthority_WithDataAnnotationMessage_NotNullReferenceException()
    {
        using ServiceProvider provider = BuildProvider(Environments.Production, new Dictionary<string, string?>());

        IOptions<AuthOptions> options = provider.GetRequiredService<IOptions<AuthOptions>>();
        Action act = () => _ = options.Value;

        OptionsValidationException exception = act.Should().Throw<OptionsValidationException>().Which;
        exception.Failures.Should()
            .Contain(failure => failure.Contains("Authority", StringComparison.OrdinalIgnoreCase),
                "the operator needs to know which configuration key is missing");
    }

    [Fact]
    public void AddOidcAuthentication_Should_RejectHttpAuthority_OutsideDevelopment()
    {
        using ServiceProvider provider = BuildProvider(
            Environments.Production,
            new Dictionary<string, string?>
            {
                ["Auth:Authority"] = "http://insecure.example.com/realms/test",
                ["Auth:Audience"] = "uniplus",
            });

        IOptions<AuthOptions> options = provider.GetRequiredService<IOptions<AuthOptions>>();
        Action act = () => _ = options.Value;

        OptionsValidationException exception = act.Should().Throw<OptionsValidationException>().Which;
        exception.Failures.Should()
            .Contain(failure => failure.Contains("HTTPS", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AddOidcAuthentication_Should_AcceptHttpsAuthority_OutsideDevelopment()
    {
        using ServiceProvider provider = BuildProvider(
            Environments.Production,
            new Dictionary<string, string?>
            {
                ["Auth:Authority"] = "https://idp.example.com/realms/test",
                ["Auth:Audience"] = "uniplus",
            });

        IOptions<AuthOptions> options = provider.GetRequiredService<IOptions<AuthOptions>>();
        Action act = () => _ = options.Value;

        act.Should().NotThrow();
    }

    [Fact]
    public void AddOidcAuthentication_Should_AcceptHttpAuthority_InDevelopment()
    {
        using ServiceProvider provider = BuildProvider(
            Environments.Development,
            new Dictionary<string, string?>
            {
                ["Auth:Authority"] = "http://localhost:8080/realms/test",
                ["Auth:Audience"] = "uniplus",
            });

        IOptions<AuthOptions> options = provider.GetRequiredService<IOptions<AuthOptions>>();
        Action act = () => _ = options.Value;

        act.Should().NotThrow();
    }

    private static ServiceProvider BuildProvider(string environmentName, IDictionary<string, string?> configValues)
    {
        ServiceCollection services = new();
        services.AddLogging();

        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        services.AddOidcAuthentication(configuration, environment);

        return services.BuildServiceProvider();
    }
}
