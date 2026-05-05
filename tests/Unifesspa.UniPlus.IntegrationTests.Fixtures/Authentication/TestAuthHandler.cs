namespace Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Authentication;

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
    public const string CpfHeader = "X-Test-Cpf";
    public const string NomeSocialHeader = "X-Test-Nome-Social";

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

        string? cpf = GetOptionalHeaderValue(CpfHeader);
        if (cpf is not null)
        {
            claims.Add(new Claim("cpf", cpf));
        }

        string? nomeSocial = GetOptionalHeaderValue(NomeSocialHeader);
        if (nomeSocial is not null)
        {
            claims.Add(new Claim("nomeSocial", nomeSocial));
        }

        if (roles.Length > 0)
        {
            claims.Add(new Claim("realm_access", JsonSerializer.Serialize(new { roles })));
        }

        ClaimsIdentity identity = new(claims, SchemeName);
        ClaimsPrincipal principal = new(identity);
        AuthenticationTicket ticket = new(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    /// <summary>
    /// Espelha o body problem+json emitido pelo JwtBearer em produção (ver
    /// <see cref="AuthenticationProblemDetailsWriter"/>). Sem este override o
    /// handler base só seta status 401 com body vazio, divergindo do contrato
    /// real e mascarando regressões em testes de integração.
    /// </summary>
    protected override Task HandleChallengeAsync(AuthenticationProperties properties) =>
        AuthenticationProblemDetailsWriter.WriteUnauthorizedAsync(Context);

    /// <summary>
    /// Espelha o body problem+json emitido pelo JwtBearer em produção quando a
    /// AuthorizationMiddleware nega acesso a um principal autenticado.
    /// </summary>
    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        AuthenticationProblemDetailsWriter.WriteForbiddenAsync(Context);

    private string GetHeaderValue(string headerName, string fallbackValue)
    {
        string? value = Request.Headers[headerName];
        return string.IsNullOrWhiteSpace(value) ? fallbackValue : value;
    }

    private string? GetOptionalHeaderValue(string headerName)
    {
        string? value = Request.Headers[headerName];
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private string[] GetHeaderValues(string headerName)
    {
        string? value = Request.Headers[headerName];
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }
}
