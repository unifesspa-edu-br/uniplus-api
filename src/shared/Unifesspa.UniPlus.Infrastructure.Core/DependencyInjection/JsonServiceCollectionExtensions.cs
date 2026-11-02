namespace Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.DependencyInjection;

public static class JsonServiceCollectionExtensions
{
    /// <summary>
    /// Aplica as mesmas opções JSON aos controllers (MVC) e às Minimal APIs, que usam
    /// pipelines distintos. <see cref="JsonNumberHandling.Strict"/> recusa número enviado
    /// como texto e faz o gerador de OpenAPI publicar o campo numérico só como número,
    /// sem a variante <c>string</c> com <c>pattern</c>.
    /// </summary>
    public static IServiceCollection AddUniPlusJsonOptions(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
            Aplicar(options.JsonSerializerOptions));
        services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
            Aplicar(options.SerializerOptions));

        return services;
    }

    private static void Aplicar(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.NumberHandling = JsonNumberHandling.Strict;
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    }
}
