namespace Unifesspa.UniPlus.IntegrationTests.Shared.Authentication;

using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Authentication handler for integration tests. Accepts a fixed bearer token
/// and builds a <see cref="ClaimsPrincipal"/> from request headers so tests can
/// drive the user identity without issuing real JWTs.
/// </summary>
public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";
    public const string AuthorizationScheme = "Bearer";
    public const string TokenValue = "test-token";
    public const string UserIdHeader = "X-Test-User-Id";
    public const string NameHeader = "X-Test-Name";
    public const string EmailHeader = "X-Test-Email";
    public const string RolesHeader = "X-Test-Roles";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? authorization = Request.Headers.Authorization;
        if (string.IsNullOrWhiteSpace(authorization))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!authorization.Equals($"{AuthorizationScheme} {TokenValue}", StringComparison.Ordinal))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid test token."));
        }

        string[] roles = GetHeaderValues(RolesHeader);
        List<Claim> claims =
        [
            new("sub", GetHeaderValue(UserIdHeader, "test-user-id")),
            new("name", GetHeaderValue(NameHeader, "Test User")),
            new("email", GetHeaderValue(EmailHeader, "test@unifesspa.edu.br")),
        ];

        if (roles.Length > 0)
        {
            claims.Add(new Claim("realm_access", JsonSerializer.Serialize(new { roles })));
        }

        ClaimsIdentity identity = new(claims, SchemeName);
        ClaimsPrincipal principal = new(identity);
        AuthenticationTicket ticket = new(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private string GetHeaderValue(string headerName, string fallbackValue)
    {
        string? value = Request.Headers[headerName];
        return string.IsNullOrWhiteSpace(value) ? fallbackValue : value;
    }

    private string[] GetHeaderValues(string headerName)
    {
        string? value = Request.Headers[headerName];
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }
}
