namespace Unifesspa.UniPlus.Infrastructure.Core.Cors;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
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
    // cross-origin só expõe sete headers "seguros" — e ETag não é um deles. Sem esta lista,
    // o servidor emite o ETag corretamente, o browser o recebe, e o fetch() do frontend
    // simplesmente NÃO O ENXERGA: response.headers.get('ETag') devolve null.
    //
    // - ETag: a precondição da sessão editorial de retificação (ADR-0110 D5). O cliente lê
    //   o tag daqui e o devolve no If-Match da próxima mutação. Sem expô-lo, toda edição
    //   sob retificação sairia 428 no browser — correta no servidor, inoperável na SPA.
    // - Idempotency-Replayed: diagnóstico de replay (ADR-0027); o frontend distingue
    //   "executou agora" de "resposta gravada".
    // - Link e X-Page-Size: a navegação por cursor vive inteiramente neles. Sem expô-los, uma
    //   aplicação de origem cruzada recebe a página e não tem como pedir a seguinte — o corpo é
    //   um array puro, e o endereço de continuação só existe no header.
    //
    // Header que só um módulo emite NÃO entra aqui: esta lista é compartilhada por todos os
    // deployables, e um nome de recurso de um módulo nela faz os outros anunciarem, no preflight,
    // um header que nunca emitem. Quem tem header próprio o declara no seu composition root, pelo
    // parâmetro de AddCorsConfiguration.
    private static readonly string[] DefaultExposedHeaders =
    [
        "ETag",
        "Idempotency-Replayed",
        "Link",
        "X-Page-Size",
    ];

    /// <summary>
    /// Binds <see cref="CorsOptions"/> and registers the default CORS policy.
    /// Outside Development, startup fails if <see cref="CorsOptions.AllowedOrigins"/> is empty.
    /// </summary>
    /// <param name="exposedHeadersAdicionais">
    /// Headers de resposta específicos da aplicação que está sendo composta, somados aos comuns.
    /// É por aqui que um módulo declara o header que só ele emite, sem impô-lo aos demais.
    /// </param>
    public static IServiceCollection AddCorsConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        params string[] exposedHeadersAdicionais)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(exposedHeadersAdicionais);

        string[] exposedHeaders = exposedHeadersAdicionais.Length == 0
            ? DefaultExposedHeaders
            : [.. DefaultExposedHeaders, .. exposedHeadersAdicionais];

        services.AddOptions<CorsOptions>()
            .Bind(configuration.GetSection(CorsOptions.SectionName))
            .Validate(
                options => environment.IsDevelopment() || options.AllowedOrigins.Count > 0,
                "CORS AllowedOrigins must be configured outside Development. Set 'Cors:AllowedOrigins' with the list of trusted frontend origins.")
            .ValidateOnStart();

        services.AddCors();

        services.AddOptions<FrameworkCorsOptions>()
            .Configure<IOptions<CorsOptions>>((frameworkOptions, ourOptionsAccessor) =>
            {
                CorsOptions opts = ourOptionsAccessor.Value;
                frameworkOptions.AddPolicy(
                    DefaultPolicyName,
                    builder => ConfigurePolicy(builder, opts, environment, exposedHeaders));
            });

        return services;
    }

    /// <summary>
    /// Applies the default CORS policy to the application pipeline.
    /// </summary>
    public static IApplicationBuilder UseCorsConfiguration(this IApplicationBuilder app) =>
        app.UseCors(DefaultPolicyName);

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
