namespace Unifesspa.UniPlus.Infrastructure.Core.Authentication;

using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Json;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>
/// Resolves authenticated user data from the current HTTP context.
/// </summary>
public sealed partial class HttpUserContext : IUserContext
{
    private static readonly StringComparer RoleComparer = StringComparer.OrdinalIgnoreCase;
    private static readonly string[] UserIdClaimCandidates = [ClaimTypes.NameIdentifier, OidcClaims.Sub];
    private static readonly string[] NameClaimCandidates = [ClaimTypes.Name, OidcClaims.Name, OidcClaims.PreferredUsername];
    private static readonly string[] EmailClaimCandidates = [ClaimTypes.Email, OidcClaims.Email];
    private static readonly string[] DirectRoleClaimTypes = [ClaimTypes.Role, KeycloakClaims.Role, KeycloakClaims.Roles];

    private readonly ClaimsPrincipal? _user;
    private readonly ILogger<HttpUserContext> _logger;
    private readonly Lazy<IReadOnlyList<string>> _roles;
    private readonly ConcurrentDictionary<string, IReadOnlyList<string>> _resourceRolesCache = new(StringComparer.Ordinal);

    public HttpUserContext(
        IHttpContextAccessor httpContextAccessor,
        ILogger<HttpUserContext> logger)
    {
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        ArgumentNullException.ThrowIfNull(logger);

        _user = httpContextAccessor.HttpContext?.User;
        _logger = logger;
        _roles = new Lazy<IReadOnlyList<string>>(ResolveRoles);
    }

    public bool IsAuthenticated => _user?.Identity?.IsAuthenticated == true;

    public string? UserId => GetFirstClaimValue(UserIdClaimCandidates);

    public string? Name => GetFirstClaimValue(NameClaimCandidates);

    public string? Email => GetFirstClaimValue(EmailClaimCandidates);

    public string? Cpf => GetSingleClaimValue(UniPlusClaims.Cpf);

    public string? NomeSocial => GetSingleClaimValue(UniPlusClaims.NomeSocial);

    public IReadOnlyList<string> Roles => _roles.Value;

    public bool HasRole(string role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);

        return Roles.Contains(role, RoleComparer);
    }

    public IReadOnlyList<string> GetResourceRoles(string resourceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);

        return _resourceRolesCache.GetOrAdd(resourceName, ResolveResourceRoles);
    }

    private IReadOnlyList<string> ResolveResourceRoles(string resourceName) =>
        ParseRolesFromJsonClaim(
                KeycloakClaims.ResourceAccess,
                _user?.FindFirst(KeycloakClaims.ResourceAccess)?.Value,
                resourceName)
            .Distinct(RoleComparer)
            .ToArray();

    private string? GetFirstClaimValue(string[] claimTypes)
    {
        if (_user is null)
        {
            return null;
        }

        foreach (string claimType in claimTypes)
        {
            string? value = _user.FindFirst(claimType)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private string? GetSingleClaimValue(string claimType)
    {
        string? value = _user?.FindFirst(claimType)?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private string[] ResolveRoles() =>
        EnumerateDirectRoleClaims()
            .Concat(ParseRolesFromJsonClaim(
                KeycloakClaims.RealmAccess,
                _user?.FindFirst(KeycloakClaims.RealmAccess)?.Value,
                resourceName: null))
            .Where(static role => !string.IsNullOrWhiteSpace(role))
            .Distinct(RoleComparer)
            .ToArray();

    private IEnumerable<string> EnumerateDirectRoleClaims()
    {
        if (_user is null)
        {
            yield break;
        }

        foreach (string claimType in DirectRoleClaimTypes)
        {
            foreach (Claim claim in _user.FindAll(claimType))
            {
                yield return claim.Value;
            }
        }
    }

    private string[] ParseRolesFromJsonClaim(string claimName, string? claimValue, string? resourceName)
    {
        if (string.IsNullOrWhiteSpace(claimValue))
        {
            return [];
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(claimValue);
            JsonElement scope = document.RootElement;

            if (resourceName is not null &&
                (!scope.TryGetProperty(resourceName, out scope) || scope.ValueKind != JsonValueKind.Object))
            {
                return [];
            }

            if (!scope.TryGetProperty(KeycloakClaims.Roles, out JsonElement rolesArray) ||
                rolesArray.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return rolesArray
                .EnumerateArray()
                .Select(static role => role.GetString())
                .OfType<string>()
                .ToArray();
        }
        catch (JsonException ex)
        {
            LogMalformedClaim(_logger, claimName, resourceName, ex);
            return [];
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Falha ao fazer parse do claim {ClaimName} (recurso {ResourceName}); roles ignoradas.")]
    private static partial void LogMalformedClaim(
        ILogger logger,
        string claimName,
        string? resourceName,
        Exception exception);
}
