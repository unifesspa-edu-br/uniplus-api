namespace Unifesspa.UniPlus.Infrastructure.Core.Profile;

/// <summary>
/// Response payload for <c>GET /api/profile/me</c>. Exposes the authenticated user's
/// identity alongside institutional attributes (<c>cpf</c>, <c>nomeSocial</c>) that
/// the IdP emits via the <c>uniplus-profile</c> scope.
/// </summary>
/// <param name="UserId">Subject identifier (Keycloak <c>sub</c> claim).</param>
/// <param name="Name">Display name, falling back to <c>preferred_username</c> when absent.</param>
/// <param name="Email">Email address, when present in the token.</param>
/// <param name="Cpf">CPF exposed by the <c>uniplus-profile</c> scope; <see langword="null"/> when absent.</param>
/// <param name="NomeSocial">Social name exposed by the <c>uniplus-profile</c> scope; <see langword="null"/> when absent.</param>
/// <param name="Roles">Realm roles resolved from the access token claims.</param>
/// <param name="Timestamp">Server-side clock read when the response was produced.</param>
public sealed record UserProfileResponse(
    string? UserId,
    string? Name,
    string? Email,
    string? Cpf,
    string? NomeSocial,
    IReadOnlyList<string> Roles,
    DateTimeOffset Timestamp);
