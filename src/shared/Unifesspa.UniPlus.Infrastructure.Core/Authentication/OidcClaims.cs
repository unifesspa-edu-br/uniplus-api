namespace Unifesspa.UniPlus.Infrastructure.Core.Authentication;

/// <summary>
/// Standard claims defined by the OpenID Connect Core 1.0 specification.
/// Portable across any OIDC-compliant IdP (Keycloak, Auth0, Okta, Azure AD, Gov.br).
/// </summary>
/// <remarks>
/// Spec: <see href="https://openid.net/specs/openid-connect-core-1_0.html#StandardClaims"/>.
/// </remarks>
internal static class OidcClaims
{
    public const string Sub = "sub";
    public const string Name = "name";
    public const string PreferredUsername = "preferred_username";
    public const string Email = "email";
}
