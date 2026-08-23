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
    /// Keycloak <c>clientId</c> of this resource server. Distinct from
    /// <see cref="Audience"/> by design (ADR-0010): the audience is the shared
    /// <c>uniplus</c> claim carried by every access token, while the client id
    /// stays <c>uniplus-api</c> and is the key under which the token nests this
    /// API's roles (<c>resource_access.{clientId}.roles</c>). Reading roles under
    /// the audience finds nothing and denies every decision.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public required string ClientId { get; init; }

    /// <summary>
    /// Clock skew tolerance for token lifetime validation. Default 30s absorbs
    /// NTP drift between replicas without opening a meaningful window for
    /// expired tokens to be accepted.
    /// </summary>
    public TimeSpan ClockSkew { get; init; } = TimeSpan.FromSeconds(30);
}
