namespace Unifesspa.UniPlus.Infrastructure.Core.Authentication;

using Microsoft.AspNetCore.Authentication.JwtBearer;

/// <summary>
/// Composes <see cref="JwtBearerEvents.OnChallenge"/> and
/// <see cref="JwtBearerEvents.OnForbidden"/> so 401/403 responses carry
/// <c>application/problem+json</c> bodies (RFC 9457) instead of the
/// default empty challenge body.
/// </summary>
/// <remarks>
/// The default <see cref="JwtBearerHandler"/> short-circuits the response
/// before <c>StatusCodePagesMiddleware</c> or <c>ExceptionHandlerMiddleware</c>
/// can act, so <c>services.AddProblemDetails()</c> alone is not enough.
/// We hook <c>OnChallenge</c>/<c>OnForbidden</c>, mark the challenge as
/// handled, and write the body via <see cref="AuthenticationProblemDetailsWriter"/>
/// so production and test handlers produce byte-equivalent payloads.
///
/// Reference: <see href="https://github.com/dotnet/aspnetcore/issues/44100"/>.
/// </remarks>
internal static class JwtBearerProblemDetailsEvents
{
    /// <summary>
    /// Wires problem+json writers into the <see cref="JwtBearerEvents.OnChallenge"/>
    /// and <see cref="JwtBearerEvents.OnForbidden"/> hooks. Existing handlers are
    /// preserved (composition, not replacement).
    /// </summary>
    public static JwtBearerEvents WithProblemDetails(this JwtBearerEvents events)
    {
        ArgumentNullException.ThrowIfNull(events);

        Func<JwtBearerChallengeContext, Task> previousChallenge = events.OnChallenge;
        events.OnChallenge = async context =>
        {
            await previousChallenge(context).ConfigureAwait(false);

            if (context.Handled || context.Response.HasStarted)
                return;

            // HandleResponse desliga o caminho default do JwtBearerHandler,
            // que normalmente popula WWW-Authenticate. AuthenticationProblemDetailsWriter
            // re-emite o header (RFC 7235 §4.1 / RFC 9110 §11.6.1 exigem em 401),
            // alinhado com ADR-0034.
            context.HandleResponse();
            await AuthenticationProblemDetailsWriter
                .WriteUnauthorizedAsync(context.HttpContext)
                .ConfigureAwait(false);
        };

        Func<ForbiddenContext, Task> previousForbidden = events.OnForbidden;
        events.OnForbidden = async context =>
        {
            await previousForbidden(context).ConfigureAwait(false);

            if (context.Response.HasStarted)
                return;

            await AuthenticationProblemDetailsWriter
                .WriteForbiddenAsync(context.HttpContext)
                .ConfigureAwait(false);
        };

        return events;
    }
}
