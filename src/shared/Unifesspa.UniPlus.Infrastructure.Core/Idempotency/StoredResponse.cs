namespace Unifesspa.UniPlus.Infrastructure.Core.Idempotency;

using System.Text.Json;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Primitives;

/// <summary>
/// A resposta que o cache de idempotência guarda e reproduz: o status, o corpo e os poucos headers que sobrevivem ao replay.
/// </summary>
internal static class StoredResponse
{
    private const string ReplayHeader = "Idempotency-Replayed";
    private const string ContentType = "Content-Type";
    private const string Location = "Location";
    private const string ETag = "ETag";

    private static readonly string[] RestoredHeaders = [Location, ETag];

    /// <summary>
    /// Serializa os headers que o replay devolve: Content-Type, Location e ETag. Os demais ficam de fora por dependerem do
    /// contexto (Set-Cookie, traceId). O ETag é a precondição da chamada seguinte numa sessão editorial (ADR-0110 D5): um
    /// replay que devolvesse 201 sem ele deixaria o cliente sem como mutar. Ele descreve o estado que esta resposta gravada
    /// representa, que é exatamente o que o replay reproduz.
    /// </summary>
    public static string SerializeHeaders(HttpResponse response)
    {
        Dictionary<string, string> stored = new(StringComparer.OrdinalIgnoreCase);

        if (response.ContentType is { Length: > 0 } contentType)
        {
            stored[ContentType] = contentType;
        }

        foreach (string name in RestoredHeaders)
        {
            CopyIfPresent(response.Headers, name, stored);
        }

        return JsonSerializer.Serialize(stored);
    }

    /// <summary>Define como resultado da requisição a resposta guardada, devolvida verbatim.</summary>
    public static void Replay(ResourceExecutingContext context, IdempotencyEntry entry, byte[] body)
    {
        Dictionary<string, string>? storedHeaders = string.IsNullOrEmpty(entry.ResponseHeadersJson)
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, string>>(entry.ResponseHeadersJson);

        SetResult(context, entry.ResponseStatus ?? StatusCodes.Status200OK, body, storedHeaders?.GetValueOrDefault(ContentType));

        SetReplayHeaders(context.HttpContext.Response.Headers, storedHeaders);
    }

    // O header do replay não é normativo no draft, mas deixa o cliente distinguir um retry da primeira execução.
    private static void SetReplayHeaders(IHeaderDictionary responseHeaders, Dictionary<string, string>? storedHeaders)
    {
        responseHeaders[ReplayHeader] = "true";

        foreach (string name in RestoredHeaders)
        {
            if (storedHeaders is not null && storedHeaders.TryGetValue(name, out string? value))
            {
                responseHeaders[name] = value;
            }
        }
    }

    // O 204, ou a resposta sem corpo nem Content-Type guardado, volta como StatusCodeResult: forçar um FileContentResult
    // com "application/json" violaria o RFC 9110 §6.4.5 (NoContent não tem Content-Type). Os demais voltam como bytes
    // verbatim, sem decodificar, o que serve a JSON e a qualquer conteúdo binário.
    private static void SetResult(ResourceExecutingContext context, int status, byte[] body, string? contentType)
    {
        if (body.Length == 0 && contentType is null)
        {
            context.Result = new StatusCodeResult(status);
            return;
        }

        context.Result = new FileContentResult(body, contentType ?? "application/json") { FileDownloadName = null };
        context.HttpContext.Response.StatusCode = status;
    }

    private static void CopyIfPresent(IHeaderDictionary source, string name, Dictionary<string, string> target)
    {
        if (source.TryGetValue(name, out StringValues values) && values.Count > 0 && !string.IsNullOrEmpty(values[0]))
        {
            target[name] = values[0]!;
        }
    }
}
