namespace Unifesspa.UniPlus.Infrastructure.Core.Authentication;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging;

/// <summary>
/// Emits structured warnings for 401 flows in the JWT Bearer pipeline so operators
/// can diagnose authentication failures by correlation id instead of blind 401s.
/// </summary>
internal static partial class JwtBearerLoggingEvents
{
    /// <summary>
    /// Wires <see cref="JwtBearerEvents.OnAuthenticationFailed"/> and
    /// <see cref="JwtBearerEvents.OnChallenge"/> into the provided <paramref name="events"/>.
    /// Existing handlers are chained — this method composes, does not replace.
    /// </summary>
    public static JwtBearerEvents WithStructuredLogging(this JwtBearerEvents events, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(logger);

        Func<AuthenticationFailedContext, Task> previousFailed = events.OnAuthenticationFailed;
        events.OnAuthenticationFailed = async context =>
        {
            LogAuthenticationFailed(logger, context.Exception.GetType().Name, context.Exception);
            await previousFailed(context).ConfigureAwait(false);
        };

        Func<JwtBearerChallengeContext, Task> previousChallenge = events.OnChallenge;
        events.OnChallenge = async context =>
        {
            string descricao = string.IsNullOrEmpty(context.ErrorDescription)
                ? context.AuthenticateFailure?.Message ?? "no-description"
                : context.ErrorDescription;
            LogChallenge(logger, context.Error ?? "no-error", descricao);
            await previousChallenge(context).ConfigureAwait(false);
        };

        return events;
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "JWT authentication failed: {ExceptionType}")]
    private static partial void LogAuthenticationFailed(ILogger logger, string exceptionType, Exception exception);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "JWT challenge emitted: {Error} — {ErrorDescription}")]
    private static partial void LogChallenge(ILogger logger, string error, string errorDescription);
}
