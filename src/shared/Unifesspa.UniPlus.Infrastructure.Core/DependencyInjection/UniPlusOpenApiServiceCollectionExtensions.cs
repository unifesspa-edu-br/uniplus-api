namespace Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using OpenApi;

/// <summary>
/// Registra o pipeline de transformers Uni+ + um documento OpenAPI nomeado
/// (<paramref name="documentName"/>). Cada módulo chama em seu Program.cs com
/// seu próprio nome (ex.: <c>"selecao"</c>, <c>"ingresso"</c>); transformers
/// são reutilizados (<c>TryAddSingleton</c>).
/// </summary>
public static class UniPlusOpenApiServiceCollectionExtensions
{
    public static IServiceCollection AddUniPlusOpenApi(
        this IServiceCollection services,
        string documentName,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentName);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<UniPlusOpenApiOptions>()
            .Bind(configuration.GetSection(UniPlusOpenApiOptions.SectionName))
            .Validate(
                static o => Uri.TryCreate(o.ContactUrl, UriKind.Absolute, out _)
                    && Uri.TryCreate(o.ProductionServerUrl, UriKind.Absolute, out _)
                    && Uri.TryCreate(o.StagingServerUrl, UriKind.Absolute, out _),
                "UniPlus:OpenApi — ContactUrl/ProductionServerUrl/StagingServerUrl precisam ser URIs absolutas.")
            .ValidateOnStart();

        services.TryAddSingleton<UniPlusInfoTransformer>();
        services.TryAddSingleton<UniPlusOperationTransformer>();
        services.TryAddSingleton<UniPlusSchemaTransformer>();

        services.AddOpenApi(documentName, options =>
        {
            options.AddDocumentTransformer<UniPlusInfoTransformer>();
            options.AddOperationTransformer<UniPlusOperationTransformer>();
            options.AddSchemaTransformer<UniPlusSchemaTransformer>();
        });

        return services;
    }
}
