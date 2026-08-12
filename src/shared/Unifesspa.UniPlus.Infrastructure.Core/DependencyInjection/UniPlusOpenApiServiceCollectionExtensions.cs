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
                    && Uri.TryCreate(o.StagingServerUrl, UriKind.Absolute, out _)
                    // Opcional, mas se vier tem de ser utilizável: uma URL relativa ou
                    // malformada aqui vira um servidor que a interface seleciona por padrão
                    // e contra o qual nenhuma chamada funciona.
                    && (string.IsNullOrWhiteSpace(o.LocalServerUrl)
                        || Uri.TryCreate(o.LocalServerUrl, UriKind.Absolute, out _)),
                "UniPlus:OpenApi — ContactUrl/ProductionServerUrl/StagingServerUrl precisam ser URIs absolutas, "
                + "e LocalServerUrl (quando definida) também.")
            .ValidateOnStart();

        services.TryAddSingleton<UniPlusInfoTransformer>();
        services.TryAddSingleton<UniPlusOperationTransformer>();
        services.TryAddSingleton<CursorPaginationOperationTransformer>();
        services.TryAddSingleton<PrecondicaoOperationTransformer>();
        services.TryAddSingleton<IdempotenciaOperationTransformer>();
        services.TryAddSingleton<AuthorizationOperationTransformer>();
        services.TryAddSingleton<VendorMediaTypeOperationTransformer>();
        services.TryAddSingleton<BearerSecuritySchemeDocumentTransformer>();
        services.TryAddSingleton<PaginationOrphanSchemaDocumentTransformer>();
        services.TryAddSingleton<UniPlusSchemaTransformer>();

        services.AddOpenApi(documentName, options =>
        {
            options.AddDocumentTransformer<UniPlusInfoTransformer>();
            options.AddOperationTransformer<UniPlusOperationTransformer>();
            options.AddOperationTransformer<CursorPaginationOperationTransformer>();
            options.AddOperationTransformer<PrecondicaoOperationTransformer>();
            options.AddOperationTransformer<IdempotenciaOperationTransformer>();
            options.AddOperationTransformer<AuthorizationOperationTransformer>();

            // Depois do UniPlusOperationTransformer, que coage os 4xx/5xx para
            // application/problem+json: este reescreve só as respostas de sucesso, e a ordem
            // deixa claro que os dois tratam de faixas de status disjuntas.
            options.AddOperationTransformer<VendorMediaTypeOperationTransformer>();
            options.AddDocumentTransformer<BearerSecuritySchemeDocumentTransformer>();
            options.AddDocumentTransformer<PaginationOrphanSchemaDocumentTransformer>();
            options.AddSchemaTransformer<UniPlusSchemaTransformer>();
        });

        return services;
    }
}
