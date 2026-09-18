namespace Unifesspa.UniPlus.Infrastructure.Core.ReverseProxy;

/// <summary>
/// Bound options describing which networks may speak for the client through forwarded
/// headers. Outside Development, <see cref="TrustedNetworks"/> must contain at least one
/// entry — otherwise startup fails.
/// </summary>
public sealed class ReverseProxyOptions
{
    public const string SectionName = "ReverseProxy";

    /// <summary>
    /// Networks, in CIDR notation, whose requests may carry <c>X-Forwarded-Proto</c>
    /// (e.g., <c>10.42.0.0/16</c> for the pod network that fronts the API).
    /// A request arriving from outside every listed network has its forwarded headers
    /// ignored, not rejected.
    /// </summary>
    public IReadOnlyList<string> TrustedNetworks { get; init; } = [];
}
