namespace Unifesspa.UniPlus.Infrastructure.Core.Authorization;

using Microsoft.AspNetCore.Authorization;

/// <summary>
/// Requirement de autorização baseada em recurso: o caller pode escrever num
/// <c>IAreaScopedEntity</c> se administra a área <c>Proprietario</c> do recurso,
/// ou se é <c>plataforma-admin</c> (ADR-0057). Avaliado por
/// <see cref="AreaScopedAuthorizationHandler"/> contra o recurso passado em
/// <c>IAuthorizationService.AuthorizeAsync</c>.
/// </summary>
public sealed class RequireAreaProprietarioRequirement : IAuthorizationRequirement;
