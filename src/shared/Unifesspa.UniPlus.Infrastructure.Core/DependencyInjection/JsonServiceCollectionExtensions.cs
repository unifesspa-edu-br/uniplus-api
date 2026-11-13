namespace Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Instantes;
using Unifesspa.UniPlus.Kernel.Tempo;

public static class JsonServiceCollectionExtensions
{
    /// <summary>
    /// Aplica as mesmas opções JSON aos controllers (MVC) e às Minimal APIs, que usam
    /// pipelines distintos. <see cref="JsonNumberHandling.Strict"/> recusa número enviado
    /// como texto e faz o gerador de OpenAPI publicar o campo numérico só como número,
    /// sem a variante <c>string</c> com <c>pattern</c>. O instante que chega no corpo, na rota, na query
    /// ou no cabeçalho é truncado em microssegundos, a precisão que o banco persiste.
    /// </summary>
    public static IServiceCollection AddUniPlusJsonOptions(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Configure<JsonOptions>(options =>
            Aplicar(options.JsonSerializerOptions));
        services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
            Aplicar(options.SerializerOptions));
        services.Configure<MvcOptions>(InserirAntesDoBinderSimples);

        return services;
    }

    private static void InserirAntesDoBinderSimples(MvcOptions options)
    {
        SimpleTypeModelBinderProvider simples = options.ModelBinderProviders.OfType<SimpleTypeModelBinderProvider>().Single();

        options.ModelBinderProviders.Insert(
            options.ModelBinderProviders.IndexOf(simples),
            new InstanteEmMicrossegundosModelBinderProvider());
    }

    private static void Aplicar(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.NumberHandling = JsonNumberHandling.Strict;
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new InstanteEmMicrossegundosJsonConverter());
    }
}
