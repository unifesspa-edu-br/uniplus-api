namespace Unifesspa.UniPlus.Infrastructure.Core.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Authentication;

/// <summary>
/// Probes the Keycloak OIDC discovery endpoint (<c>/.well-known/openid-configuration</c>).
/// Fails the readiness check when the authority is unreachable so Kubernetes does not
/// route traffic to a pod that cannot validate access tokens.
/// </summary>
public sealed class KeycloakHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<AuthOptions> _authOptions;

    public KeycloakHealthCheck(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<AuthOptions> authOptions)
    {
        _httpClientFactory = httpClientFactory;
        _authOptions = authOptions;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        AuthOptions options = _authOptions.CurrentValue;
        Uri discoveryUri = new($"{options.Authority.TrimEnd('/')}/.well-known/openid-configuration");

        try
        {
            using HttpClient client = _httpClientFactory.CreateClient(nameof(KeycloakHealthCheck));
            using HttpResponseMessage response = await client.GetAsync(discoveryUri, cancellationToken).ConfigureAwait(false);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy($"Keycloak discovery respondeu {(int)response.StatusCode}.")
                : HealthCheckResult.Unhealthy($"Keycloak discovery respondeu {(int)response.StatusCode}.");
        }
#pragma warning disable CA1031 // Captura genérica intencional em health check
        catch (Exception ex)
#pragma warning restore CA1031
        {
            return HealthCheckResult.Unhealthy("Keycloak discovery inacessível.", ex);
        }
    }
}
