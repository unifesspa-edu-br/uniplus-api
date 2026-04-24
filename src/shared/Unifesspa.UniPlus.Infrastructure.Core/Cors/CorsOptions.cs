namespace Unifesspa.UniPlus.Infrastructure.Core.Cors;

/// <summary>
/// Bound options for CORS configuration. Outside Development, <see cref="AllowedOrigins"/>
/// must contain at least one entry — otherwise startup fails.
/// </summary>
public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    /// <summary>
    /// Explicit list of origins allowed to call the API (e.g., <c>https://app.unifesspa.edu.br</c>).
    /// </summary>
    public IReadOnlyList<string> AllowedOrigins { get; init; } = [];

    /// <summary>
    /// When <see langword="true"/>, allows all HTTP methods. Default constrains to the
    /// methods the API actually exposes (GET/POST/PUT/DELETE/PATCH/OPTIONS).
    /// </summary>
    public bool AllowAnyMethod { get; init; }

    /// <summary>
    /// When <see langword="true"/>, allows all request headers. Default restricts to
    /// <c>Content-Type</c>, <c>Authorization</c>, <c>Accept</c>, <c>X-Requested-With</c>.
    /// </summary>
    public bool AllowAnyHeader { get; init; }

    /// <summary>
    /// When <see langword="true"/> and <see cref="AllowedOrigins"/> is non-empty, sets the
    /// <c>Access-Control-Allow-Credentials</c> header. Relevant for cookie-based flows
    /// (refresh tokens in HttpOnly cookies); JWT Bearer in the <c>Authorization</c> header
    /// does not require this.
    /// </summary>
    public bool AllowCredentials { get; init; }
}
