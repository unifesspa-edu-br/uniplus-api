namespace Unifesspa.UniPlus.Infrastructure.Core.Authentication;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>
/// Extension methods for configuring Keycloak authentication services.
/// </summary>
public static class KeycloakAuthenticationConfiguration
{
    /// <summary>
    /// Adds Keycloak JWT authentication and the current user context services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="environment">The hosting environment — controls HTTPS metadata enforcement.</param>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddKeycloakAuth(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        string? authority = configuration["Auth:Authority"];
        string? audience = configuration["Auth:Audience"];

        if (string.IsNullOrWhiteSpace(authority))
        {
            throw new InvalidOperationException(
                "Auth:Authority configuration is required. " +
                "Please add it to appsettings.json with the OIDC provider URL.");
        }

        if (string.IsNullOrWhiteSpace(audience))
        {
            throw new InvalidOperationException(
                "Auth:Audience configuration is required. " +
                "Please add it to appsettings.json with the client ID or API audience.");
        }

        // Em produção o OIDC discovery (/.well-known/openid-configuration) deve ser obtido via HTTPS.
        // Permitir http:// só em Development evita um foot-gun silencioso se alguém configurar
        // Auth:Authority com http:// em Production.
        bool requireHttps = !environment.IsDevelopment();
        if (requireHttps && authority.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Auth:Authority must use HTTPS outside Development. Current value: {authority}.");
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.Audience = audience;
                options.RequireHttpsMetadata = requireHttps;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,

                    // Default do framework é 5min. 30s é apertado mas tolera drift NTP
                    // entre réplicas em Kubernetes, evitando 401 intermitente em bordas de expiração.
                    ClockSkew = TimeSpan.FromSeconds(30),
                };

                options.MapInboundClaims = false;
            });

        services.AddHttpContextAccessor();
        services.AddScoped<IUserContext, HttpUserContext>();
        services.AddAuthorization();

        return services;
    }
}
