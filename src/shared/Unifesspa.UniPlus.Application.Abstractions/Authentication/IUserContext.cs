namespace Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>
/// Provides access to the authenticated user context extracted from the current request.
/// </summary>
public interface IUserContext
{
    /// <summary>
    /// Gets a value indicating whether the current request carries an
    /// authenticated principal (JWT validated, ClaimsIdentity.IsAuthenticated).
    /// Anonymous requests (no <c>Authorization</c> header, or auth failure)
    /// return <see langword="false"/> and all other properties default to
    /// <see langword="null"/>/empty.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Gets the authenticated user's identifier.
    /// </summary>
    string? UserId { get; }

    /// <summary>
    /// Gets the authenticated user's display name.
    /// </summary>
    string? Name { get; }

    /// <summary>
    /// Gets the authenticated user's email address.
    /// </summary>
    string? Email { get; }

    /// <summary>
    /// Gets the authenticated user's CPF, when exposed by the IdP via the <c>uniplus-profile</c> scope.
    /// </summary>
    string? Cpf { get; }

    /// <summary>
    /// Gets the authenticated user's social name, when exposed by the IdP via the <c>uniplus-profile</c> scope.
    /// </summary>
    string? NomeSocial { get; }

    /// <summary>
    /// Gets the authenticated user's roles.
    /// </summary>
    IReadOnlyList<string> Roles { get; }

    /// <summary>
    /// Determines whether the user has the specified role.
    /// </summary>
    /// <param name="role">The role to check.</param>
    /// <returns><see langword="true"/> when the user has the role; otherwise, <see langword="false"/>.</returns>
    bool HasRole(string role);

    /// <summary>
    /// Gets roles scoped to a specific resource (resource_access claim in Keycloak).
    /// </summary>
    /// <param name="resourceName">The resource name.</param>
    /// <returns>The list of roles associated with the resource.</returns>
    IReadOnlyList<string> GetResourceRoles(string resourceName);
}
