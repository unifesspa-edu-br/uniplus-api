namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Cors;

using AwesomeAssertions;

using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Unifesspa.UniPlus.Infrastructure.Core.Cors;

using FrameworkCorsOptions = Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions;

/// <summary>
/// Monta a política CORS padrão como a aplicação a monta, com as ações dos controllers informados
/// no lugar das que o MVC descobriria.
/// </summary>
internal static class PoliticaCorsDeTeste
{
    internal static CorsPolicy Construir(bool allowAnyHeader, params Type[] controllers)
    {
        ServiceCollection services = new();
        services.AddSingleton(NullLoggerFactory.Instance);
        services.AddSingleton(AcoesDos(controllers));
        services.AddCorsConfiguration(Configuracao(allowAnyHeader), EmProducao());

        using ServiceProvider provider = services.BuildServiceProvider();
        FrameworkCorsOptions options = provider.GetRequiredService<IOptions<FrameworkCorsOptions>>().Value;

        CorsPolicy? policy = options.GetPolicy(CorsConfiguration.DefaultPolicyName);
        policy.Should().NotBeNull();
        return policy!;
    }

    private static IConfiguration Configuracao(bool allowAnyHeader) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cors:AllowedOrigins:0"] = "https://selecao.standalone.portaluni.com.br",
                ["Cors:AllowAnyHeader"] = allowAnyHeader.ToString(),
            })
            .Build();

    private static IHostEnvironment EmProducao()
    {
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns("Production");
        return environment;
    }

    private static IActionDescriptorCollectionProvider AcoesDos(Type[] controllers)
    {
        ControllerActionDescriptor[] acoes =
        [
            .. controllers
                .SelectMany(static controller => controller.GetMethods().Where(static m => m.DeclaringType != typeof(object)))
                .Select(static metodo => new ControllerActionDescriptor { MethodInfo = metodo }),
        ];

        IActionDescriptorCollectionProvider provider = Substitute.For<IActionDescriptorCollectionProvider>();
        provider.ActionDescriptors.Returns(new ActionDescriptorCollection([.. acoes.Cast<ActionDescriptor>()], version: 1));
        return provider;
    }
}
