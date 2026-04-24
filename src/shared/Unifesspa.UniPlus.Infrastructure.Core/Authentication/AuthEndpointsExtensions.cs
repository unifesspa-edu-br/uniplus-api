namespace Unifesspa.UniPlus.Infrastructure.Core.Authentication;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>
/// Extension methods that map shared authentication endpoints onto the application pipeline.
/// </summary>
public static class AuthEndpointsExtensions
{
    /// <summary>
    /// Maps the shared <c>/api/auth</c> endpoints (currently the authenticated-user probe <c>/me</c>).
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public static IEndpointRouteBuilder MapSharedAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder authGroup = endpoints.MapGroup("/api/auth")
            .WithTags("Auth")
            .RequireAuthorization();

        authGroup.MapGet("/me", (IUserContext userContext, TimeProvider clock) =>
                Results.Ok(new AuthenticatedUserResponse(
                    userContext.UserId,
                    userContext.Name,
                    userContext.Email,
                    userContext.Roles,
                    clock.GetUtcNow())))
            .WithName("GetAuthenticatedUser")
            .WithSummary("Returns the authenticated user")
            .WithDescription("Returns user information extracted from the access token. Requires authentication.")
            .Produces<AuthenticatedUserResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        return endpoints;
    }
}
