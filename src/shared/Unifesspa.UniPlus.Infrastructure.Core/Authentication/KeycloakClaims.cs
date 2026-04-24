namespace Unifesspa.UniPlus.Infrastructure.Core.Authentication;

/// <summary>
/// JWT claim names produced by Keycloak. Keep in sync with the realm's client scope
/// configuration. Use these constants instead of string literals so the names stay
/// discoverable and rename-safe across the codebase.
/// </summary>
internal static class KeycloakClaims
{
    public const string Sub = "sub";
    public const string Name = "name";
    public const string PreferredUsername = "preferred_username";
    public const string Email = "email";
    public const string Role = "role";
    public const string Roles = "roles";
    public const string RealmAccess = "realm_access";
    public const string ResourceAccess = "resource_access";
}
