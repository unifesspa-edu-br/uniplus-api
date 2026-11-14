namespace Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

using System.Reflection;

using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Cors;
using Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

/// <summary>
/// Os dois lados da promessa de header de uma aplicação composta: o que os endpoints declaram no
/// contrato e o que a política CORS deixa o navegador entregar ao JavaScript.
/// </summary>
public static class HeadersDoContrato
{
    /// <summary>
    /// Nomes que os endpoints da aplicação prometem no contrato: os headers próprios de cada rota
    /// e o selo de entidade, declarado pelo seu próprio atributo.
    /// </summary>
    public static HashSet<string> Declarados(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.GetRequiredService<EndpointDataSource>().Endpoints
            .Select(static endpoint => endpoint.Metadata.GetMetadata<ControllerActionDescriptor>())
            .OfType<ControllerActionDescriptor>()
            .Select(static descritor => descritor.MethodInfo)
            .Distinct()
            .SelectMany(NomesDeclarados)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Nomes que a política padrão da aplicação autoriza o JavaScript de outra origem a ler.
    /// </summary>
    public static IReadOnlyCollection<string> ExpostosPelaPoliticaPadrao(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions opcoes =
            services.GetRequiredService<IOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>>().Value;
        CorsPolicy politica = opcoes.GetPolicy(CorsConfiguration.DefaultPolicyName)
            ?? throw new InvalidOperationException("A política CORS padrão não está registrada.");

        return [.. politica.ExposedHeaders];
    }

    private static IEnumerable<string> NomesDeclarados(MethodInfo acao)
    {
        foreach (EmiteHeaderAttribute header in acao.GetCustomAttributes<EmiteHeaderAttribute>(inherit: true))
        {
            yield return header.Nome;
        }

        if (acao.GetCustomAttribute<EmiteETagAttribute>(inherit: true) is not null)
        {
            yield return "ETag";
        }
    }
}
