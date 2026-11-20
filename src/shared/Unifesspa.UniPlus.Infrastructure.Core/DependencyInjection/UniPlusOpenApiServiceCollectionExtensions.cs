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
            .AddOpenApi(documentName, ConfigurarDocumento);
    }

    // Os nomes dos componentes e os componentes dos códigos pertencem a um documento: cada um tem o seu, para que o mesmo
    // nome possa ser de tipos diferentes em documentos diferentes.
    private static void ConfigurarDocumento(OpenApiOptions options)
    {
        NomesDosComponentes nomes = new();
        ComponentesDosCodigos codigos = new(nomes);

        options.CreateSchemaReferenceId = nomes.Nome;
        AdicionarTransformersDeDocumento(options, codigos);
        AdicionarTransformersDeOperacao(options, codigos);
        AdicionarTransformersDeCorpoEDeResposta(options);
        AdicionarTransformersDeSchema(options, codigos);
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

    private static IServiceCollection AddTransformersDoOpenApi(this IServiceCollection services) =>
        services
            .AddSingletonsDosTransformersDeDocumento()
            .AddSingletonsDosTransformersDeOperacao()
            .AddSingletonsDosTransformersDeSchema();

    private static IServiceCollection AddSingletonsDosTransformersDeDocumento(this IServiceCollection services)
    {
        services.TryAddSingleton<UniPlusInfoTransformer>();
        services.TryAddSingleton<BearerSecuritySchemeDocumentTransformer>();
        services.TryAddSingleton<PaginationOrphanSchemaDocumentTransformer>();
        services.TryAddSingleton<ProblemDetailsExtensionsDocumentTransformer>();
        return services;
    }

    private static IServiceCollection AddSingletonsDosTransformersDeOperacao(this IServiceCollection services)
    {
        services.TryAddSingleton<UniPlusOperationTransformer>();
        services.TryAddSingleton<CursorPaginationOperationTransformer>();
        services.TryAddSingleton<HeadersDeRespostaOperationTransformer>();
        services.TryAddSingleton<PrecondicaoObrigatoriaOperationTransformer>();
        services.TryAddSingleton<IdempotenciaOperationTransformer>();
        services.TryAddSingleton<EsperaEsgotadaPelaTravaOperationTransformer>();
        services.TryAddSingleton<AuthorizationOperationTransformer>();
        services.TryAddSingleton<CorpoMultipartOperationTransformer>();
        services.TryAddSingleton<CorpoJsonOperationTransformer>();
        services.TryAddSingleton<RespostaJsonOperationTransformer>();
        services.TryAddSingleton<VendorMediaTypeOperationTransformer>();
        return services;
    }

    private static IServiceCollection AddSingletonsDosTransformersDeSchema(this IServiceCollection services)
    {
        services.TryAddSingleton<UniPlusSchemaTransformer>();
        services.TryAddSingleton<InstanteSchemaTransformer>();
        return services;
    }

    private static void AdicionarTransformersDeDocumento(OpenApiOptions options, ComponentesDosCodigos codigos)
    {
        options.AddDocumentTransformer<UniPlusInfoTransformer>();
        options.AddDocumentTransformer<BearerSecuritySchemeDocumentTransformer>();
        options.AddDocumentTransformer<PaginationOrphanSchemaDocumentTransformer>();
        options.AddDocumentTransformer<ProblemDetailsExtensionsDocumentTransformer>();

        // Depois de quem registrou: os transformers de schema já viram todo tipo do documento, e este escreve as referências.
        options.AddDocumentTransformer(codigos);
    }

    private static void AdicionarTransformersDeOperacao(OpenApiOptions options, ComponentesDosCodigos codigos)
    {
        options.AddOperationTransformer<UniPlusOperationTransformer>();
        options.AddOperationTransformer<CursorPaginationOperationTransformer>();
        options.AddOperationTransformer<HeadersDeRespostaOperationTransformer>();
        options.AddOperationTransformer<PrecondicaoObrigatoriaOperationTransformer>();
        options.AddOperationTransformer<IdempotenciaOperationTransformer>();
        options.AddOperationTransformer<EsperaEsgotadaPelaTravaOperationTransformer>();
        options.AddOperationTransformer<AuthorizationOperationTransformer>();
        options.AddOperationTransformer(new AceitaCodigosDeOperationTransformer(codigos));
    }

    private static void AdicionarTransformersDeCorpoEDeResposta(OpenApiOptions options)
    {
        options.AddOperationTransformer<CorpoMultipartOperationTransformer>();
        options.AddOperationTransformer<CorpoJsonOperationTransformer>();
        options.AddOperationTransformer<RespostaJsonOperationTransformer>();

        // Depois do RespostaJsonOperationTransformer, de quem copia o schema do corpo para as vendor MIME, e depois do
        // UniPlusOperationTransformer, que coage os 4xx/5xx para application/problem+json: este reescreve só as
        // respostas de sucesso, e a ordem deixa claro que os dois tratam de faixas de status disjuntas.
        options.AddOperationTransformer<VendorMediaTypeOperationTransformer>();
    }

    private static void AdicionarTransformersDeSchema(OpenApiOptions options, ComponentesDosCodigos codigos)
    {
        options.AddSchemaTransformer<UniPlusSchemaTransformer>();
        options.AddSchemaTransformer<InstanteSchemaTransformer>();
        options.AddSchemaTransformer(new FormatosDeCodigoSchemaTransformer(codigos));
        options.AddSchemaTransformer(new AceitaCodigosDeSchemaTransformer(codigos));
    }
}
