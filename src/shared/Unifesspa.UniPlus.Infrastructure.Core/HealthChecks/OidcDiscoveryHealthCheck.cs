namespace Unifesspa.UniPlus.Infrastructure.Core.HealthChecks;

using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Authentication;

/// <summary>
/// Probes the OIDC discovery endpoint (<c>/.well-known/openid-configuration</c>, RFC 8414).
/// Fails the readiness check when the authority is unreachable so Kubernetes does not route
/// traffic to a pod that cannot validate access tokens. Provider-agnostic — works against
/// Keycloak, Auth0, Okta, Azure AD, Gov.br or any OIDC-compliant IdP.
/// </summary>
public sealed class OidcDiscoveryHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<AuthOptions> _authOptions;

    public OidcDiscoveryHealthCheck(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<AuthOptions> authOptions)
    {
        _httpClientFactory = httpClientFactory;
        _authOptions = authOptions;
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Health check must isolate downstream failures and report Unhealthy instead of propagating exceptions to the readiness pipeline.")]
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        AuthOptions options = _authOptions.CurrentValue;
        Uri discoveryUri = new($"{options.Authority.TrimEnd('/')}/.well-known/openid-configuration");

        try
        {
            using HttpClient client = _httpClientFactory.CreateClient(nameof(OidcDiscoveryHealthCheck));
            using HttpResponseMessage response = await client.GetAsync(discoveryUri, cancellationToken).ConfigureAwait(false);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy($"OIDC discovery respondeu {(int)response.StatusCode}.")
                : HealthCheckResult.Unhealthy($"OIDC discovery respondeu {(int)response.StatusCode}.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("OIDC discovery inacessível.", ex);
        }
    }
}
