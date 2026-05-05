namespace Unifesspa.UniPlus.Infrastructure.Core.Authentication;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using HealthChecks;

/// <summary>
/// Extension methods for configuring OIDC/JWT Bearer authentication.
/// Validation is OIDC-standard (issuer, audience, signing key, lifetime); the Keycloak
/// reference in ADR-008 is about the current provider, not the code contract.
/// </summary>
public static class OidcAuthenticationConfiguration
{
    /// <summary>
    /// Binds <see cref="AuthOptions"/> and configures JWT Bearer authentication against the OIDC authority.
    /// Options are validated at application startup (required fields, URL format, HTTPS outside Development).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="environment">The hosting environment — controls HTTPS metadata enforcement.</param>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddOidcAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        bool requireHttps = !environment.IsDevelopment();

        services.AddOptions<AuthOptions>()
            .Bind(configuration.GetSection(AuthOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                options => !requireHttps
                    || options.Authority is not string authority
                    || !authority.StartsWith("http://", StringComparison.OrdinalIgnoreCase),
                "Auth:Authority must use HTTPS outside Development.")
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<AuthOptions>, ILoggerFactory>((jwtOptions, authAccessor, loggerFactory) =>
            {
                AuthOptions auth = authAccessor.Value;

                jwtOptions.Authority = auth.Authority;
                jwtOptions.Audience = auth.Audience;
                jwtOptions.RequireHttpsMetadata = requireHttps;
                jwtOptions.MapInboundClaims = false;

                jwtOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = auth.ClockSkew,
                };

                jwtOptions.Events.WithStructuredLogging(
                    loggerFactory.CreateLogger("Unifesspa.UniPlus.Authentication.JwtBearer"));
            });

        services.AddHttpContextAccessor();
        services.AddScoped<IUserContext, HttpUserContext>();
        // Variante required: lê do mesmo IUserContext mas falha-rápido em
        // request anônima. Use em handlers/controllers protegidos por
        // [Authorize] onde anônimo é estado impossível.
        services.AddScoped<IRequiredUserContext>(sp =>
            new RequiredUserContext(sp.GetRequiredService<IUserContext>()));

        services.AddHttpClient(nameof(OidcDiscoveryHealthCheck));
        services.AddHealthChecks()
            .AddCheck<OidcDiscoveryHealthCheck>(
                name: "oidc-discovery",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready", "auth"]);

        // TODO(#26-followup): registrar policies por role/permissão (Admin, Gestor, Avaliador, Candidato).
        services.AddAuthorization();

        return services;
    }
}
