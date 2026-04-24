namespace Unifesspa.UniPlus.Infrastructure.Core.Cors;

using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Fluent extensions on <see cref="CorsPolicyBuilder"/> that encapsulate the
/// per-dimension decisions (origins, methods, headers, credentials) so policy
/// assembly reads as a single expressive chain.
/// </summary>
public static class CorsPolicyBuilderExtensions
{
    /// <summary>
    /// Applies the configured origins. Falls back to <c>AllowAnyOrigin</c> only in
    /// Development when no explicit origins are declared — production callers fail
    /// earlier via <see cref="CorsOptions"/> validation.
    /// </summary>
    public static CorsPolicyBuilder WithConfiguredOrigins(
        this CorsPolicyBuilder builder,
        IReadOnlyList<string> allowedOrigins,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(allowedOrigins);
        ArgumentNullException.ThrowIfNull(environment);

        if (allowedOrigins.Count > 0)
        {
            return builder.WithOrigins([.. allowedOrigins]);
        }

        return environment.IsDevelopment()
            ? builder.AllowAnyOrigin()
            : builder;
    }

    /// <summary>
    /// Applies <c>AllowAnyMethod</c> when the caller opts in; otherwise restricts to
    /// <paramref name="defaultMethods"/>.
    /// </summary>
    public static CorsPolicyBuilder WithConfiguredMethods(
        this CorsPolicyBuilder builder,
        bool allowAny,
        string[] defaultMethods)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(defaultMethods);

        return allowAny
            ? builder.AllowAnyMethod()
            : builder.WithMethods(defaultMethods);
    }

    /// <summary>
    /// Applies <c>AllowAnyHeader</c> when the caller opts in; otherwise restricts to
    /// <paramref name="defaultHeaders"/>.
    /// </summary>
    public static CorsPolicyBuilder WithConfiguredHeaders(
        this CorsPolicyBuilder builder,
        bool allowAny,
        string[] defaultHeaders)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(defaultHeaders);

        return allowAny
            ? builder.AllowAnyHeader()
            : builder.WithHeaders(defaultHeaders);
    }

    /// <summary>
    /// Enables credentials only when explicit origins are configured (CORS spec
    /// forbids <c>Access-Control-Allow-Credentials: true</c> with wildcard origin).
    /// </summary>
    public static CorsPolicyBuilder WithCredentialsIfConfigured(
        this CorsPolicyBuilder builder,
        bool allowCredentials,
        bool hasExplicitOrigins)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return allowCredentials && hasExplicitOrigins
            ? builder.AllowCredentials()
            : builder;
    }
}
