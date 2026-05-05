namespace Unifesspa.UniPlus.Infrastructure.Core.Profile;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>
/// Extension methods that map shared profile endpoints onto the application pipeline.
/// </summary>
public static class ProfileEndpointsExtensions
{
    /// <summary>
    /// Maps the shared <c>/api/profile</c> endpoints (currently the authenticated-user
    /// profile <c>/me</c>, exposing CPF and NomeSocial).
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public static IEndpointRouteBuilder MapSharedProfileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder profileGroup = endpoints.MapGroup("/api/profile")
            .WithTags("Profile")
            .RequireAuthorization();

        profileGroup.MapGet("/me", (IUserContext userContext, TimeProvider clock) =>
                Results.Ok(new UserProfileResponse(
                    userContext.UserId,
                    userContext.Name,
                    userContext.Email,
                    userContext.Cpf,
                    userContext.NomeSocial,
                    userContext.Roles,
                    clock.GetUtcNow())))
            .WithName("GetAuthenticatedUserProfile")
            .WithSummary("Retorna o perfil do usuário autenticado")
            .WithDescription("Retorna atributos de identidade e institucionais (CPF, NomeSocial) extraídos do access token. Requer autenticação.")
            .Produces<UserProfileResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return endpoints;
    }
}
