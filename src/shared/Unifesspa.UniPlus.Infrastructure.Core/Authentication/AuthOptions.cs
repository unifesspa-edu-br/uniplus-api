namespace Unifesspa.UniPlus.Infrastructure.Core.Authentication;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Bound options for JWT/Keycloak authentication. Validation runs at startup
/// via <c>ValidateDataAnnotations().ValidateOnStart()</c>.
/// </summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// OIDC authority (Keycloak realm URL). Must be HTTPS outside Development.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    [Url]
    public required string Authority { get; init; }

    /// <summary>
    /// Expected audience (<c>aud</c>) claim of the incoming access token.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public required string Audience { get; init; }

    /// <summary>
    /// Clock skew tolerance for token lifetime validation. Default 30s absorbs
    /// NTP drift between replicas without opening a meaningful window for
    /// expired tokens to be accepted.
    /// </summary>
    public TimeSpan ClockSkew { get; init; } = TimeSpan.FromSeconds(30);
}
