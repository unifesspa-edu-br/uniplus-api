namespace Unifesspa.UniPlus.Infrastructure.Core.Authentication;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

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
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddKeycloakAuth(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

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

        bool requireHttps = !authority.StartsWith("http://", StringComparison.OrdinalIgnoreCase);

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
                    ClockSkew = TimeSpan.Zero,
                };

                options.MapInboundClaims = false;
            });

        services.AddHttpContextAccessor();
        services.AddScoped<IUserContext, HttpUserContext>();
        services.AddAuthorization();

        return services;
    }
}
