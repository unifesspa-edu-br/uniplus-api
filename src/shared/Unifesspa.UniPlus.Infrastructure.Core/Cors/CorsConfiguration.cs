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
    private static readonly string[] DefaultHeaders = ["Content-Type", "Authorization", "Accept", "X-Requested-With"];

    /// <summary>
    /// Binds <see cref="CorsOptions"/> and registers the default CORS policy.
    /// Outside Development, startup fails if <see cref="CorsOptions.AllowedOrigins"/> is empty.
    /// </summary>
    public static IServiceCollection AddCorsConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

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
                    builder => ConfigurePolicy(builder, opts, environment));
            });

        return services;
    }

    /// <summary>
    /// Applies the default CORS policy to the application pipeline.
    /// </summary>
    public static IApplicationBuilder UseCorsConfiguration(this IApplicationBuilder app) =>
        app.UseCors(DefaultPolicyName);

    private static void ConfigurePolicy(CorsPolicyBuilder builder, CorsOptions options, IHostEnvironment environment) =>
        builder
            .WithConfiguredOrigins(options.AllowedOrigins, environment)
            .WithConfiguredMethods(options.AllowAnyMethod, DefaultMethods)
            .WithConfiguredHeaders(options.AllowAnyHeader, DefaultHeaders)
            .WithCredentialsIfConfigured(options.AllowCredentials, hasExplicitOrigins: options.AllowedOrigins.Count > 0);
}
