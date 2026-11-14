namespace Unifesspa.UniPlus.Infrastructure.Core.Idempotency;

using System.Text.Json;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Primitives;

/// <summary>
/// A resposta que o cache de idempotência guarda e reproduz: o status, o corpo e os poucos headers que sobrevivem ao replay.
/// </summary>
internal static class RespostaGuardada
{
    private const string HeaderDoReplay = "Idempotency-Replayed";
    private const string ContentType = "Content-Type";
    private const string Location = "Location";
    private const string ETag = "ETag";

    private static readonly string[] HeadersRestaurados = [Location, ETag];

    /// <summary>
    /// Serializa os headers que o replay devolve: Content-Type, Location e ETag. Os demais ficam de fora por dependerem do
    /// contexto (Set-Cookie, traceId). O ETag é a precondição da chamada seguinte numa sessão editorial (ADR-0110 D5): um
    /// replay que devolvesse 201 sem ele deixaria o cliente sem como mutar. Ele descreve o estado que esta resposta gravada
    /// representa, que é exatamente o que o replay reproduz.
    /// </summary>
    public static string SerializarCabecalhos(HttpResponse resposta)
    {
        Dictionary<string, string> guardados = new(StringComparer.OrdinalIgnoreCase);

        if (resposta.ContentType is { Length: > 0 } contentType)
        {
            guardados[ContentType] = contentType;
        }

        foreach (string nome in HeadersRestaurados)
        {
            CopiarSePresente(resposta.Headers, nome, guardados);
        }

        return JsonSerializer.Serialize(guardados);
    }

    /// <summary>Define como resultado da requisição a resposta guardada, devolvida verbatim.</summary>
    public static void Reproduzir(ResourceExecutingContext context, IdempotencyEntry entrada, byte[] corpo)
    {
        Dictionary<string, string>? cabecalhos = string.IsNullOrEmpty(entrada.ResponseHeadersJson)
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, string>>(entrada.ResponseHeadersJson);

        DefinirResultado(context, entrada.ResponseStatus ?? StatusCodes.Status200OK, corpo, cabecalhos?.GetValueOrDefault(ContentType));

        DefinirCabecalhosDoReplay(context.HttpContext.Response.Headers, cabecalhos);
    }

    // O header do replay não é normativo no draft, mas deixa o cliente distinguir um retry da primeira execução.
    private static void DefinirCabecalhosDoReplay(IHeaderDictionary headers, Dictionary<string, string>? cabecalhos)
    {
        headers[HeaderDoReplay] = "true";

        foreach (string nome in HeadersRestaurados)
        {
            if (cabecalhos is not null && cabecalhos.TryGetValue(nome, out string? valor))
            {
                headers[nome] = valor;
            }
        }
    }

    // O 204, ou a resposta sem corpo nem Content-Type guardado, volta como StatusCodeResult: forçar um FileContentResult
    // com "application/json" violaria o RFC 9110 §6.4.5 (NoContent não tem Content-Type). Os demais voltam como bytes
    // verbatim, sem decodificar, o que serve a JSON e a qualquer conteúdo binário.
    private static void DefinirResultado(ResourceExecutingContext context, int status, byte[] corpo, string? contentType)
    {
        if (corpo.Length == 0 && contentType is null)
        {
            context.Result = new StatusCodeResult(status);
            return;
        }

        context.Result = new FileContentResult(corpo, contentType ?? "application/json") { FileDownloadName = null };
        context.HttpContext.Response.StatusCode = status;
    }

    private static void CopiarSePresente(IHeaderDictionary origem, string nome, Dictionary<string, string> destino)
    {
        if (origem.TryGetValue(nome, out StringValues valores) && valores.Count > 0 && !string.IsNullOrEmpty(valores[0]))
        {
            destino[nome] = valores[0]!;
        }
    }
}
