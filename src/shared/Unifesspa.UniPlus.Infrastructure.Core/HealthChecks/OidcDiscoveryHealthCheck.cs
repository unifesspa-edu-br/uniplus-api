namespace Unifesspa.UniPlus.Infrastructure.Core.HealthChecks;

using System.Diagnostics.CodeAnalysis;

using Authentication;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

/// <summary>
/// Probes the OIDC discovery endpoint (<c>/.well-known/openid-configuration</c>, RFC 8414).
/// Reports <see cref="HealthStatus.Degraded"/> when the authority is unreachable: the state stays visible in
/// <c>/health</c>, but it is not part of the readiness probe, so an identity provider outage does not take
/// out routes that need no authentication. Provider-agnostic — works against
/// Keycloak, Auth0, Okta, Azure AD, Gov.br or any OIDC-compliant IdP.
/// </summary>
public sealed class OidcDiscoveryHealthCheck : IHealthCheck
{
    /// <summary>Espera máxima pelo provedor, para o health completo responder antes de qualquer timeout de sonda.</summary>
    public static readonly TimeSpan LimiteDeEspera = TimeSpan.FromSeconds(2);

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
        Justification = "Health check must isolate downstream failures and report Degraded instead of propagating exceptions to the readiness pipeline.")]
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
                : HealthCheckResult.Degraded($"OIDC discovery respondeu {(int)response.StatusCode}.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded("OIDC discovery inacessível.", ex);
        }
    }
}
