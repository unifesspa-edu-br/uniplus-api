namespace Unifesspa.UniPlus.Infrastructure.Core.Authentication;

/// <summary>
/// Keycloak-proprietary claims (not part of OIDC Core). Shape of these claims is defined
/// by Keycloak itself — if the IdP is ever replaced (Auth0, Okta, Azure AD), these claim
/// names change and the parsing in <see cref="HttpUserContext"/> must be revisited.
/// </summary>
/// <remarks>
/// Docs: <see href="https://www.keycloak.org/docs/latest/server_admin/#protocol-mappers"/>.
/// </remarks>
internal static class KeycloakClaims
{
    public const string Role = "role";
    public const string Roles = "roles";
    public const string RealmAccess = "realm_access";
    public const string ResourceAccess = "resource_access";
}
