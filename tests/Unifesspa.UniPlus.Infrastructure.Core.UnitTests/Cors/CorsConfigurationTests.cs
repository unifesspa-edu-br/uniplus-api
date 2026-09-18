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

    [Fact]
    public void Policy_ExpoeOsHeadersDaNavegacaoPaginada()
    {
        // Uma resposta de origem cruzada só entrega ao JavaScript sete headers considerados
        // seguros, e nenhum dos nossos é um deles. A navegação por cursor vive INTEIRAMENTE em
        // Link e X-Page-Size: sem expô-los, o SPA recebe a página e não tem como pedir a seguinte
        // — o corpo é um array puro, e o endereço de continuação só existe no header.
        CorsPolicy policy = BuildDefaultPolicy(allowAnyHeader: false);

        policy.ExposedHeaders.Should().Contain(["Link", "X-Page-Size"],
            because: "sem eles, response.headers.get() devolve null e a paginação some para quem "
                + "consome do navegador — sem erro, sem status diferente e sem nada a investigar.");
    }

    [Fact]
    public void Policy_ExpoeOSeloEOReplayDeIdempotencia()
    {
        // Os dois que já estavam na lista. Entram aqui para que uma mudança futura não os remova
        // sem ninguém perceber: o ETag é a precondição da próxima mutação, e sem lê-lo toda edição
        // sob retificação sairia 428 no navegador.
        CorsPolicy policy = BuildDefaultPolicy(allowAnyHeader: false);

        policy.ExposedHeaders.Should().Contain(["ETag", "Idempotency-Replayed"]);
    }

    [Fact]
    public void Policy_ComHeaderProprioDeModulo_SomaSemPerderOsComuns()
    {
        // Header que só um deployable emite não pertence à lista compartilhada: declará-lo lá faria
        // os outros anunciarem, no preflight, um header que nunca emitem. O composition root de
        // quem o emite o acrescenta — e acrescentar não pode custar os comuns.
        CorsPolicy policy = BuildDefaultPolicy(allowAnyHeader: false, "X-Certames-Em-Breve");

        policy.ExposedHeaders.Should().Contain("X-Certames-Em-Breve");
        policy.ExposedHeaders.Should().Contain(["ETag", "Idempotency-Replayed", "Link", "X-Page-Size"]);
    }

    private static CorsPolicy BuildDefaultPolicy(bool allowAnyHeader, params string[] exposedHeadersAdicionais)
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
        services.AddCorsConfiguration(configuration, environment, exposedHeadersAdicionais);

        using ServiceProvider provider = services.BuildServiceProvider();
        FrameworkCorsOptions options = provider.GetRequiredService<IOptions<FrameworkCorsOptions>>().Value;

        CorsPolicy? policy = options.GetPolicy(CorsConfiguration.DefaultPolicyName);
        policy.Should().NotBeNull();
        return policy!;
    }
}
