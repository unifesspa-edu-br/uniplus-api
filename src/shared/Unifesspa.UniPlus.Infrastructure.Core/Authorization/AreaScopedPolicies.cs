namespace Unifesspa.UniPlus.Infrastructure.Core.Authorization;

/// <summary>
/// Nomes das policies de autorização por área organizacional (ADR-0057).
/// </summary>
public static class AreaScopedPolicies
{
    /// <summary>
    /// Policy baseada em recurso: exige que o caller administre a área
    /// <c>Proprietario</c> do <c>IAreaScopedEntity</c>, ou seja
    /// <c>plataforma-admin</c>.
    /// </summary>
    /// <remarks>
    /// Invocada imperativamente — <c>IAuthorizationService.AuthorizeAsync(user,
    /// entidadeAreaScoped, AreaScopedPolicies.RequireAreaProprietario)</c> — e não
    /// como <c>[Authorize(Policy = ...)]</c> declarativo, porque o atributo
    /// declarativo não carrega o recurso (<c>IAreaScopedEntity</c>) que o handler
    /// precisa para decidir.
    /// </remarks>
    public const string RequireAreaProprietario = "uniplus:require-area-proprietario";
}
