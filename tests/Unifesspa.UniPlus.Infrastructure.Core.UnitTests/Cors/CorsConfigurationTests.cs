namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Cors;

using AwesomeAssertions;

using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Unifesspa.UniPlus.Infrastructure.Core.Cors;

using FrameworkCorsOptions = Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions;

public sealed class CorsConfigurationTests
{
    [Fact]
    public void Policy_QuandoAllowAnyHeaderFalso_DeveDeclararIdempotencyKey()
    {
        CorsPolicy policy = BuildDefaultPolicy(allowAnyHeader: false);

        policy.Headers.Should().Contain("Idempotency-Key",
            because: "endpoints com [RequiresIdempotencyKey] dependem desse header no preflight CORS (ADR-0027). " +
                     "Sem isto na lista, navegadores rejeitam toda criação via SPA antes da request real sair.");
    }

    [Fact]
    public void Policy_QuandoAllowAnyHeaderFalso_DeveDeclararCondicionaisHttp()
    {
        CorsPolicy policy = BuildDefaultPolicy(allowAnyHeader: false);

        policy.Headers.Should().Contain("If-Match");
        policy.Headers.Should().Contain("If-None-Match");
    }

    [Fact]
    public void Policy_QuandoAllowAnyHeaderFalso_DeveDeclararBasicosRest()
    {
        CorsPolicy policy = BuildDefaultPolicy(allowAnyHeader: false);

        policy.Headers.Should().Contain(["Content-Type", "Authorization", "Accept", "X-Requested-With"]);
    }

    [Fact]
    public void Policy_QuandoAllowAnyHeaderTrue_PreservaSemantica()
    {
        CorsPolicy policy = BuildDefaultPolicy(allowAnyHeader: true);

        policy.Headers.Should().Equal(["*"],
            because: "AllowAnyHeader=true registra wildcard único na lista (semântica do framework AspNetCore).");
    }

    private static CorsPolicy BuildDefaultPolicy(bool allowAnyHeader)
    {
        Dictionary<string, string?> settings = new()
        {
            ["Cors:AllowedOrigins:0"] = "https://selecao.standalone.portaluni.com.br",
            ["Cors:AllowAnyHeader"] = allowAnyHeader.ToString(),
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns("Production");

        ServiceCollection services = new();
        services.AddSingleton(NullLoggerFactory.Instance);
        services.AddCorsConfiguration(configuration, environment);

        using ServiceProvider provider = services.BuildServiceProvider();
        FrameworkCorsOptions options = provider.GetRequiredService<IOptions<FrameworkCorsOptions>>().Value;

        CorsPolicy? policy = options.GetPolicy(CorsConfiguration.DefaultPolicyName);
        policy.Should().NotBeNull();
        return policy!;
    }
}
