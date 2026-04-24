namespace Unifesspa.UniPlus.Infrastructure.Core.Cors;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Provides extension methods for configuring CORS (Cross-Origin Resource Sharing) policies.
/// </summary>
public static class CorsConfiguration
{
    private const string DefaultPolicyName = "DefaultCorsPolicy";

    /// <summary>
    /// Adds CORS configuration based on application settings with environment-specific policies.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="environment">The hosting environment.</param>
    /// <returns>The configured service collection for method chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when AllowedOrigins is not configured in non-development environments.</exception>
    public static IServiceCollection AddCorsConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        IConfigurationSection corsSection = configuration.GetSection("Cors");
        string[] allowedOrigins = corsSection.GetSection("AllowedOrigins").Get<string[]>() ?? [];
        bool allowAnyMethod = corsSection.GetValue<bool>("AllowAnyMethod");
        bool allowAnyHeader = corsSection.GetValue<bool>("AllowAnyHeader");
        bool allowCredentials = corsSection.GetValue<bool>("AllowCredentials");

        services.AddCors(options =>
        {
            options.AddPolicy(DefaultPolicyName, builder =>
            {
                switch (allowedOrigins.Length)
                {
                    case > 0:
                        builder.WithOrigins(allowedOrigins);

                        break;
                    default:
                        {
                            if (!environment.IsDevelopment())
                            {
                                throw new InvalidOperationException(
                                    "CORS AllowedOrigins must be configured in non-development environments. " +
                                    "Configure 'Cors:AllowedOrigins' in appsettings.json or environment variables " +
                                    "to specify which origins are allowed to access this API.");
                            }

                            builder.AllowAnyOrigin();

                            break;
                        }
                }

                switch (allowAnyMethod)
                {
                    case true:
                        builder.AllowAnyMethod();

                        break;
                    default:
                        builder.WithMethods("GET", "POST", "PUT", "DELETE", "PATCH", "OPTIONS");

                        break;
                }

                switch (allowAnyHeader)
                {
                    case true:
                        builder.AllowAnyHeader();

                        break;
                    default:
                        builder.WithHeaders("Content-Type", "Authorization", "Accept", "X-Requested-With");

                        break;
                }

                if (allowCredentials && allowedOrigins.Length > 0)
                {
                    builder.AllowCredentials();
                }
            });
        });

        return services;
    }

    /// <summary>
    /// Applies the default CORS policy to the application pipeline.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for method chaining.</returns>
    public static IApplicationBuilder UseCorsConfiguration(this IApplicationBuilder app) =>
        app.UseCors(DefaultPolicyName);
}
