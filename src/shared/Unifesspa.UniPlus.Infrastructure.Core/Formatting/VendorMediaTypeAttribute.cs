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

        // RFC 9110 §12.5.1: maior q-value vence quando há múltiplos aceitáveis.
        // OrderByDescending estável preserva ordem do header como tie-breaker.
        // Quality ausente == 1.0 (default RFC).
        List<MediaTypeHeaderValue> ordered = [.. parsed
            .OrderByDescending(static m => m.Quality ?? 1.0)];

        // RFC 9110 §12.5.1: q=0 não é apenas "ignorar" — é "excluir do match".
        // Construir primeiro a lista de exclusões para que matches por wildcard
        // ou por vendor explícito subsequentes respeitem essas exclusões.
        HashSet<int> excludedVersions = [];
        bool excludeAllViaWildcard = false;

        foreach (MediaTypeHeaderValue media in ordered)
        {
            if (media.Quality is not 0)
                continue;

            string excluded = media.MediaType.Value ?? string.Empty;
            if (IsWildcardOrJson(excluded))
            {
                excludeAllViaWildcard = true;
            }
            else if (TryMatchVendor(excluded, out int excludedVersion))
            {
                excludedVersions.Add(excludedVersion);
            }
        }

        foreach (MediaTypeHeaderValue media in ordered)
        {
            if (media.Quality is 0)
                continue;

            string mediaType = media.MediaType.Value ?? string.Empty;

            // RFC 9110 §8.3.1: media type tokens são case-insensitive.
            if (IsWildcardOrJson(mediaType))
            {
                if (excludeAllViaWildcard || excludedVersions.Contains(latest))
                    continue;

                StoreAcceptedVersion(context.HttpContext, latest);
                return;
            }

            if (TryMatchVendor(mediaType, out int requestedVersion)
                && Array.IndexOf(Versions, requestedVersion) >= 0
                && !excludedVersions.Contains(requestedVersion))
            {
                StoreAcceptedVersion(context.HttpContext, requestedVersion);
                return;
            }
        }

        WriteNotAcceptable(context);
    }

    public override void OnResultExecuting(ResultExecutingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.HttpContext.Items.TryGetValue(ResponseContextKey, out object? value)
            || value is not int version)
        {
            return;
        }

        // RFC 9457 §3: respostas de erro mantêm Content-Type
        // application/problem+json. O wire format do erro não muda com o
        // versionamento por vendor MIME — sobrescrever quebraria clientes
        // que dispatcham por content-type para tratar ProblemDetails.
        if (IsProblemDetailsResult(context.Result))
        {
            return;
        }

        string vendorMime = BuildVendorMime(Resource, version);
        context.HttpContext.Response.ContentType = vendorMime;
    }

    private static bool IsProblemDetailsResult(IActionResult result) =>
        result switch
        {
            ObjectResult { Value: ProblemDetails } => true,
            ObjectResult objectResult when objectResult.ContentTypes.Any(static ct =>
                string.Equals(ct, "application/problem+json", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ct, "application/problem+xml", StringComparison.OrdinalIgnoreCase)) => true,
            _ => false,
        };

    private static bool IsWildcardOrJson(string mediaType) =>
        string.Equals(mediaType, "*/*", StringComparison.OrdinalIgnoreCase)
        || string.Equals(mediaType, "application/*", StringComparison.OrdinalIgnoreCase)
        || string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase);

    private bool TryMatchVendor(string mediaType, out int version)
    {
        version = 0;

        // RFC 9110 §8.3.1: type/subtype são case-insensitive.
        if (!mediaType.StartsWith(VendorPrefix, StringComparison.OrdinalIgnoreCase)
            || !mediaType.EndsWith(JsonSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string inner = mediaType.Substring(VendorPrefix.Length, mediaType.Length - VendorPrefix.Length - JsonSuffix.Length);
        Match match = VendorMimeRegex().Match(inner);
        if (!match.Success)
        {
            return false;
        }

        if (!string.Equals(match.Groups["resource"].Value, Resource, StringComparison.OrdinalIgnoreCase))
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

    [GeneratedRegex(@"^(?<resource>[a-z][a-z0-9_-]*)\.v(?<version>\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex VendorMimeRegex();
}
