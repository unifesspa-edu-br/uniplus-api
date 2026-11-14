namespace Unifesspa.UniPlus.Infrastructure.Core.Cors;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using FrameworkCorsOptions = Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions;

/// <summary>
/// Extension methods for configuring CORS (Cross-Origin Resource Sharing) policies.
/// </summary>
public static class CorsConfiguration
{
    public const string DefaultPolicyName = "DefaultCorsPolicy";

    private static readonly string[] DefaultMethods = ["GET", "POST", "PUT", "DELETE", "PATCH", "OPTIONS"];

    // Headers explicitamente declarados no CORS preflight. Lista canônica:
    // qualquer header novo que o frontend precise enviar deve entrar aqui sob
    // pena de browsers rejeitarem o preflight (curl/server-to-server não sofre).
    //
    // - Content-Type, Authorization, Accept, X-Requested-With: básicos REST.
    // - Idempotency-Key: header obrigatório em POSTs com [RequiresIdempotencyKey]
    //   (ADR-0027). Sem este aqui, toda criação via SPA falha "Não foi possível
    //   conectar ao servidor" antes da request real sair.
    // - If-Match, If-None-Match: condicionais HTTP (RFC 9110); preventivos para
    //   ETags do Contrato V1 (ADR-0022).
    private static readonly string[] DefaultHeaders =
    [
        "Content-Type",
        "Authorization",
        "Accept",
        "X-Requested-With",
        "Idempotency-Key",
        "If-Match",
        "If-None-Match",
    ];

    // Headers de RESPOSTA que o browser deixa o JavaScript ler. Por default, uma resposta
    // cross-origin só expõe sete headers "seguros", e nenhum dos nossos é um deles. Sem expô-lo, o
    // servidor emite o header corretamente, o browser o recebe, e response.headers.get(...) do
    // frontend simplesmente devolve null, sem erro e sem nada a investigar.
    //
    // Esta lista traz só o que a infraestrutura compartilhada emite sem que nenhum endpoint o
    // declare. Todo header que um endpoint promete no contrato (EmiteHeader e EmiteETag) é somado
    // à política na subida de cada aplicação, a partir dos controllers que ela de fato hospeda;
    // por isso um header de um módulo só é exposto onde esse módulo é servido, e nenhuma lista
    // paralela precisa ser mantida à mão.
    //
    // - ETag: a precondição da sessão editorial de retificação (ADR-0110 D5); o cliente o lê e o
    //   devolve no If-Match da próxima mutação.
    // - Idempotency-Replayed: diagnóstico de replay (ADR-0027), emitido pelo filtro de
    //   idempotência; o frontend distingue "executou agora" de "resposta gravada".
    // - Link e X-Page-Size: a navegação por cursor vive inteiramente neles (ADR-0026); o corpo é um
    //   array puro e o endereço da página seguinte só existe no header.
    // - X-Total-Count: o total da coleção, quando a requisição o pede (ADR-0026). Sai da mesma
    //   extensão de paginação, que não pertence a módulo nenhum.
    private static readonly string[] DefaultExposedHeaders =
    [
        "ETag",
        "Idempotency-Replayed",
        "Link",
        "X-Page-Size",
        "X-Total-Count",
    ];

    /// <summary>
    /// Binds <see cref="CorsOptions"/> and registers the default CORS policy.
    /// Outside Development, startup fails if <see cref="CorsOptions.AllowedOrigins"/> is empty.
    /// </summary>
    /// <remarks>
    /// Os headers de resposta que a política expõe são os comuns mais os que os controllers da
    /// aplicação declaram no contrato. A leitura dos controllers acontece quando as opções são
    /// resolvidas, depois de o MVC conhecer todos eles.
    /// </remarks>
    public static IServiceCollection AddCorsConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        RegistrarOpcoes(services, configuration, environment);
        services.AddCors();
        RegistrarPoliticaPadrao(services, environment);

        return services;
    }

    /// <summary>
    /// Applies the default CORS policy to the application pipeline.
    /// </summary>
    public static IApplicationBuilder UseCorsConfiguration(this IApplicationBuilder app) =>
        app.UseCors(DefaultPolicyName);

    private static void RegistrarOpcoes(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment) =>
        services.AddOptions<CorsOptions>()
            .Bind(configuration.GetSection(CorsOptions.SectionName))
            .Validate(
                options => environment.IsDevelopment() || options.AllowedOrigins.Count > 0,
                "CORS AllowedOrigins must be configured outside Development. Set 'Cors:AllowedOrigins' with the list of trusted frontend origins.")
            .ValidateOnStart();

    private static void RegistrarPoliticaPadrao(IServiceCollection services, IHostEnvironment environment) =>
        services.AddOptions<FrameworkCorsOptions>()
            .Configure<IOptions<CorsOptions>, IActionDescriptorCollectionProvider>(
                (frameworkOptions, ourOptionsAccessor, acoes) =>
                {
                    CorsOptions opts = ourOptionsAccessor.Value;
                    string[] exposedHeaders = ExposedHeaders(acoes);
                    frameworkOptions.AddPolicy(
                        DefaultPolicyName,
                        builder => ConfigurePolicy(builder, opts, environment, exposedHeaders));
                });

    private static string[] ExposedHeaders(IActionDescriptorCollectionProvider acoes) =>
        [.. DefaultExposedHeaders.Union(HeadersDeclaradosNoContrato.Ler(acoes), StringComparer.OrdinalIgnoreCase)];

    private static void ConfigurePolicy(
        CorsPolicyBuilder builder,
        CorsOptions options,
        IHostEnvironment environment,
        string[] exposedHeaders) =>
        builder
            .WithConfiguredOrigins(options.AllowedOrigins, environment)
            .WithConfiguredMethods(options.AllowAnyMethod, DefaultMethods)
            .WithConfiguredHeaders(options.AllowAnyHeader, DefaultHeaders)
            .WithExposedHeaders(exposedHeaders)
            .WithCredentialsIfConfigured(options.AllowCredentials, hasExplicitOrigins: options.AllowedOrigins.Count > 0);
}
