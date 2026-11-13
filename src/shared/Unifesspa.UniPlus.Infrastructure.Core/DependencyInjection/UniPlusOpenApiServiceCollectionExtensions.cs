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

        return services
            .AddOpcoesDoOpenApi(configuration)
            .AddTransformersDoOpenApi()
            .AddOpenApi(documentName, options =>
            {
                AdicionarTransformersDeDocumento(options);
                AdicionarTransformersDeOperacao(options);
                AdicionarTransformersDeSchema(options);
            });
    }

    private static IServiceCollection AddOpcoesDoOpenApi(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<UniPlusOpenApiOptions>()
            .Bind(configuration.GetSection(UniPlusOpenApiOptions.SectionName))
            .Validate(
                ContatoEServidoresSaoUrisUtilizaveis,
                "UniPlus:OpenApi — ContactUrl/ProductionServerUrl/StagingServerUrl precisam ser URIs absolutas, "
                + "e LocalServerUrl (quando definida) também.")
            .ValidateOnStart();

        return services;
    }

    private static bool ContatoEServidoresSaoUrisUtilizaveis(UniPlusOpenApiOptions opcoes) =>
        UriAbsoluta(opcoes.ContactUrl)
        && UriAbsoluta(opcoes.ProductionServerUrl)
        && UriAbsoluta(opcoes.StagingServerUrl)
        && ServidorLocalAusenteOuUtilizavel(opcoes.LocalServerUrl);

    // Opcional, mas se vier tem de ser utilizável: uma URL relativa ou malformada aqui vira um
    // servidor que a interface seleciona por padrão e contra o qual nenhuma chamada funciona.
    private static bool ServidorLocalAusenteOuUtilizavel(string? url) =>
        string.IsNullOrWhiteSpace(url) || UriAbsoluta(url);

    private static bool UriAbsoluta(string? url) => Uri.TryCreate(url, UriKind.Absolute, out _);

    private static IServiceCollection AddTransformersDoOpenApi(this IServiceCollection services)
    {
        services.TryAddSingleton<UniPlusInfoTransformer>();
        services.TryAddSingleton<UniPlusOperationTransformer>();
        services.TryAddSingleton<CursorPaginationOperationTransformer>();
        services.TryAddSingleton<PrecondicaoOperationTransformer>();
        services.TryAddSingleton<IdempotenciaOperationTransformer>();
        services.TryAddSingleton<AuthorizationOperationTransformer>();
        services.TryAddSingleton<CorpoMultipartOperationTransformer>();
        services.TryAddSingleton<VendorMediaTypeOperationTransformer>();
        services.TryAddSingleton<BearerSecuritySchemeDocumentTransformer>();
        services.TryAddSingleton<PaginationOrphanSchemaDocumentTransformer>();
        services.TryAddSingleton<ProblemDetailsExtensionsDocumentTransformer>();
        services.TryAddSingleton<UniPlusSchemaTransformer>();
        services.TryAddSingleton<InstanteSchemaTransformer>();
        return services;
    }

    private static void AdicionarTransformersDeDocumento(OpenApiOptions options)
    {
        options.AddDocumentTransformer<UniPlusInfoTransformer>();
        options.AddDocumentTransformer<BearerSecuritySchemeDocumentTransformer>();
        options.AddDocumentTransformer<PaginationOrphanSchemaDocumentTransformer>();
        options.AddDocumentTransformer<ProblemDetailsExtensionsDocumentTransformer>();
    }

    private static void AdicionarTransformersDeOperacao(OpenApiOptions options)
    {
        options.AddOperationTransformer<UniPlusOperationTransformer>();
        options.AddOperationTransformer<CursorPaginationOperationTransformer>();
        options.AddOperationTransformer<PrecondicaoOperationTransformer>();
        options.AddOperationTransformer<IdempotenciaOperationTransformer>();
        options.AddOperationTransformer<AuthorizationOperationTransformer>();
        options.AddOperationTransformer<CorpoMultipartOperationTransformer>();

        // Depois do UniPlusOperationTransformer, que coage os 4xx/5xx para
        // application/problem+json: este reescreve só as respostas de sucesso, e a ordem
        // deixa claro que os dois tratam de faixas de status disjuntas.
        options.AddOperationTransformer<VendorMediaTypeOperationTransformer>();
    }

    private static void AdicionarTransformersDeSchema(OpenApiOptions options)
    {
        options.AddSchemaTransformer<UniPlusSchemaTransformer>();
        options.AddSchemaTransformer<InstanteSchemaTransformer>();
    }
}
