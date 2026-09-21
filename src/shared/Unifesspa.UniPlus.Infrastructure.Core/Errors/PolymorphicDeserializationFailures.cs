namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

using Microsoft.AspNetCore.Http;

/// <summary>
/// Preserva a causa estruturada de uma falha de discriminator durante a leitura do corpo.
/// </summary>
internal static class PolymorphicDeserializationFailures
{
    public const string HttpContextItemKey = "__UniPlusPolymorphicDeserializationFailure";

    public static void Record(HttpContext httpContext, string fieldName, string discriminatorName)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        httpContext.Items[HttpContextItemKey] = new Failure(fieldName, discriminatorName);
    }

    public static bool TryGet(HttpContext httpContext, out Failure failure)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (httpContext.Items.TryGetValue(HttpContextItemKey, out object? value)
            && value is Failure recorded)
        {
            failure = recorded;
            return true;
        }

        failure = default;
        return false;
    }

    internal readonly record struct Failure(string FieldName, string DiscriminatorName);
}
