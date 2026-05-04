namespace Unifesspa.UniPlus.Infrastructure.Core.Formatting;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Net.Http.Headers;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>
/// Action filter que negocia <c>application/vnd.uniplus.&lt;resource&gt;.v&lt;N&gt;+json</c>
/// no header <c>Accept</c>, conforme ADR-0028. <c>application/json</c>, <c>*/*</c>
/// ou <c>Accept</c> ausente caem para a versão mais recente (latest); versões
/// inexistentes resultam em 406 com extension <c>available_versions</c> e
/// o <c>Content-Type</c> da resposta é fixado na versão efetivamente aceita.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
[SuppressMessage(
    "Performance",
    "CA1813:Avoid unsealed attributes",
    Justification = "ASP.NET Core resolve filtros por tipo concreto via reflection; manter unsealed permite extensão controlada por subclasses específicas de recurso, sem quebrar a descoberta de filtros.")]
public partial class VendorMediaTypeAttribute : ActionFilterAttribute
{
    private const string JsonSuffix = "+json";
    private const string VendorPrefix = "application/vnd.uniplus.";
    private const string ResponseContextKey = "__UniPlusVendorMediaTypeAccepted";

    /// <summary>Identificador do recurso na vendor MIME (ex.: <c>edital</c>).</summary>
    public string Resource { get; init; } = string.Empty;

    /// <summary>Versões inteiras aceitas. A última posição é o latest usado como fallback.</summary>
#pragma warning disable CA1819 // Properties should not return arrays
    public int[] Versions { get; init; } = [];
#pragma warning restore CA1819

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrEmpty(Resource) || Versions.Length == 0)
        {
            throw new InvalidOperationException(
                $"{nameof(VendorMediaTypeAttribute)} requer Resource e Versions configurados.");
        }

        int latest = Versions[^1];
        HttpRequest request = context.HttpContext.Request;

        if (!request.Headers.TryGetValue(HeaderNames.Accept, out Microsoft.Extensions.Primitives.StringValues acceptValues)
            || acceptValues.Count == 0
            || acceptValues.All(static v => string.IsNullOrWhiteSpace(v)))
        {
            StoreAcceptedVersion(context.HttpContext, latest);
            return;
        }

        if (!MediaTypeHeaderValue.TryParseList(acceptValues, out IList<MediaTypeHeaderValue>? parsed))
        {
            WriteNotAcceptable(context);
            return;
        }

        bool sawWrongVendorVersion = false;

        foreach (MediaTypeHeaderValue media in parsed)
        {
            string mediaType = media.MediaType.Value ?? string.Empty;

            if (mediaType is "*/*" or "application/*" or "application/json")
            {
                StoreAcceptedVersion(context.HttpContext, latest);
                return;
            }

            if (TryMatchVendor(mediaType, out int requestedVersion))
            {
                if (Array.IndexOf(Versions, requestedVersion) >= 0)
                {
                    StoreAcceptedVersion(context.HttpContext, requestedVersion);
                    return;
                }

                sawWrongVendorVersion = true;
            }
        }

        if (sawWrongVendorVersion)
        {
            WriteNotAcceptable(context);
            return;
        }

        WriteNotAcceptable(context);
    }

    public override void OnResultExecuting(ResultExecutingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.HttpContext.Items.TryGetValue(ResponseContextKey, out object? value)
            && value is int version)
        {
            string vendorMime = BuildVendorMime(Resource, version);
            context.HttpContext.Response.ContentType = vendorMime;
        }
    }

    private bool TryMatchVendor(string mediaType, out int version)
    {
        version = 0;

        if (!mediaType.StartsWith(VendorPrefix, StringComparison.Ordinal)
            || !mediaType.EndsWith(JsonSuffix, StringComparison.Ordinal))
        {
            return false;
        }

        string inner = mediaType.Substring(VendorPrefix.Length, mediaType.Length - VendorPrefix.Length - JsonSuffix.Length);
        Match match = VendorMimeRegex().Match(inner);
        if (!match.Success)
        {
            return false;
        }

        if (!string.Equals(match.Groups["resource"].Value, Resource, StringComparison.Ordinal))
        {
            return false;
        }

        return int.TryParse(match.Groups["version"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out version);
    }

    private static void StoreAcceptedVersion(HttpContext httpContext, int version) =>
        httpContext.Items[ResponseContextKey] = version;

    private static string BuildVendorMime(string resource, int version) =>
        string.Create(CultureInfo.InvariantCulture, $"{VendorPrefix}{resource}.v{version}{JsonSuffix}");

    private void WriteNotAcceptable(ActionExecutingContext context)
    {
        const string code = "uniplus.contract.versao_nao_suportada";
        ProblemDetails problem = new()
        {
            Status = StatusCodes.Status406NotAcceptable,
            Type = ProblemDetailsConstants.ErrorsBaseUri + code,
            Title = "Versão de mídia não suportada",
            Detail = $"Nenhuma versão suportada pelo recurso '{Resource}' foi aceita pelo cliente.",
            Instance = $"urn:uuid:{Guid.CreateVersion7()}",
        };

        problem.Extensions["code"] = code;
        problem.Extensions["available_versions"] = Versions;
        problem.Extensions["traceId"] = Activity.Current?.TraceId.ToHexString()
            ?? Guid.CreateVersion7().ToString("N");

        context.Result = new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status406NotAcceptable,
            ContentTypes = { "application/problem+json" },
        };
    }

    [GeneratedRegex(@"^(?<resource>[a-z][a-z0-9_-]*)\.v(?<version>\d+)$")]
    private static partial Regex VendorMimeRegex();
}
