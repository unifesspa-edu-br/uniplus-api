namespace Unifesspa.UniPlus.Ingresso.API.Endpoints;

using Microsoft.AspNetCore.Authorization;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>
/// Authentication endpoints for validating and inspecting the current authenticated user.
/// </summary>
internal static class AuthEndpoints
{
    /// <summary>
    /// Maps authentication endpoints to the application.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application with mapped endpoints.</returns>
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
#pragma warning disable SA1134 // Attributes should be on separate lines - Not applicable for lambda expressions
        RouteGroupBuilder authGroup = app.MapGroup("/api/auth")
            .WithTags("Auth");

        authGroup.MapGet(
            "/me",
            [Authorize] (IUserContext userContext) =>
        {
            return Results.Ok(new
            {
                userId = userContext.UserId,
                name = userContext.Name,
                email = userContext.Email,
                roles = userContext.Roles,
                timestamp = DateTime.UtcNow,
            });
        })
        .WithName("GetAuthenticatedUser")
        .WithSummary("Returns the authenticated user")
        .WithDescription("This endpoint requires authentication and returns user information from the access token.");
#pragma warning restore SA1134

        return app;
    }
}
