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

    public string? UserId => GetFirstClaimValue(UserIdClaimCandidates);

    public string? Name => GetFirstClaimValue(NameClaimCandidates);

    public string? Email => GetFirstClaimValue(EmailClaimCandidates);

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

    private IReadOnlyList<string> ResolveResourceRoles(string resourceName)
    {
        string claimValue = _user?.FindFirst(KeycloakClaims.ResourceAccess)?.Value ?? string.Empty;
        if (string.IsNullOrWhiteSpace(claimValue))
        {
            return [];
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(claimValue);
            if (!document.RootElement.TryGetProperty(resourceName, out JsonElement resourceElement) ||
                !resourceElement.TryGetProperty(KeycloakClaims.Roles, out JsonElement rolesElement) ||
                rolesElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return rolesElement
                .EnumerateArray()
                .Select(static role => role.GetString())
                .OfType<string>()
                .Distinct(RoleComparer)
                .ToArray();
        }
        catch (JsonException ex)
        {
            LogMalformedClaim(_logger, KeycloakClaims.ResourceAccess, resourceName, ex);
            return [];
        }
    }

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

    private string[] ResolveRoles()
    {
        List<string> roles =
        [
            .. (_user?.FindAll(ClaimTypes.Role).Select(static claim => claim.Value) ?? []),
            .. (_user?.FindAll(KeycloakClaims.Role).Select(static claim => claim.Value) ?? []),
            .. (_user?.FindAll(KeycloakClaims.Roles).Select(static claim => claim.Value) ?? []),
        ];

        string? realmAccess = _user?.FindFirst(KeycloakClaims.RealmAccess)?.Value;
        if (!string.IsNullOrWhiteSpace(realmAccess))
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(realmAccess);
                if (document.RootElement.TryGetProperty(KeycloakClaims.Roles, out JsonElement realmRoles) &&
                    realmRoles.ValueKind == JsonValueKind.Array)
                {
                    roles.AddRange(
                        realmRoles
                            .EnumerateArray()
                            .Select(static role => role.GetString())
                            .OfType<string>());
                }
            }
            catch (JsonException ex)
            {
                LogMalformedClaim(_logger, KeycloakClaims.RealmAccess, resourceName: null, ex);
            }
        }

        return roles
            .Where(static role => !string.IsNullOrWhiteSpace(role))
            .Distinct(RoleComparer)
            .ToArray();
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
