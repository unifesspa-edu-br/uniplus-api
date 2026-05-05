namespace Unifesspa.UniPlus.Infrastructure.Core.Idempotency;

using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

using Cryptography;
using Errors;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Kernel.Results;

/// <summary>
/// Resource filter (roda antes de model binding e antes do action filter)
/// que implementa o protocolo Idempotency-Key conforme ADR-0027 e
/// draft-ietf-httpapi-idempotency-key-header-07. Faz lookup, replay verbatim
/// em hit-match, 422 em hit-mismatch, 409 em processing, 400 em key
/// ausente/malformada. Em miss, reserva entry, deixa o action executar e
/// completa após.
/// </summary>
/// <remarks>
/// <para><b>Por que ResourceFilter e não Middleware:</b> precisamos do
/// metadata da action (atributo <c>[RequiresIdempotencyKey]</c>) e do
/// template da rota (<c>endpoint</c> canônico) — ambos disponíveis após
/// routing, antes do model binding. ResourceFilter é o ponto canônico.</para>
/// <para><b>Body buffering:</b> o filter lê o body raw via
/// <c>EnableBuffering()</c> e <c>request.Body.Position = 0</c>, depois
/// reseta para que o model binder consuma normalmente.</para>
/// <para><b>Atomicidade vs ADR-0027:</b> reservation e completion rodam
/// em transações separadas (limitação documentada em ADR-0027 §"Negativas").
/// Janela de inconsistência é coberta por status Processing → 409 em retry
/// concorrente.</para>
/// </remarks>
public sealed partial class IdempotencyFilter : IAsyncResourceFilter
{
    [GeneratedRegex(@"^[\x21-\x7E]{1,255}$")]
    private static partial Regex KeyPrintableAsciiRegex();

    // Vírgula e ponto-vírgula são proibidos pelo draft IETF mesmo estando no
    // range ASCII printable — usados como separadores em sf-list. Espaço já é
    // rejeitado pela regex (0x20 < 0x21).
    private static readonly char[] ForbiddenKeyChars = [',', ';'];

    private const string ReplayHeader = "Idempotency-Replayed";

    private readonly IIdempotencyStore _store;
    private readonly IUniPlusEncryptionService _encryption;
    private readonly IDomainErrorMapper _errorMapper;
    private readonly TimeProvider _time;
    private readonly IUserContext _userContext;
    private readonly IdempotencyOptions _options;

    public IdempotencyFilter(
        IIdempotencyStore store,
        IUniPlusEncryptionService encryption,
        IDomainErrorMapper errorMapper,
        TimeProvider time,
        IUserContext userContext,
        IOptions<IdempotencyOptions> options)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(encryption);
        ArgumentNullException.ThrowIfNull(errorMapper);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(options);

        _store = store;
        _encryption = encryption;
        _errorMapper = errorMapper;
        _time = time;
        _userContext = userContext;
        _options = options.Value;
    }

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (!HasRequiresIdempotencyKey(context))
        {
            await next().ConfigureAwait(false);
            return;
        }

        HttpContext httpContext = context.HttpContext;

        // Header obrigatório.
        if (!httpContext.Request.Headers.TryGetValue("Idempotency-Key", out Microsoft.Extensions.Primitives.StringValues headerValues)
            || headerValues.Count == 0
            || string.IsNullOrEmpty(headerValues[0]))
        {
            ShortCircuit(context, IdempotencyDomainErrorCodes.KeyAusente,
                "Header Idempotency-Key é obrigatório neste endpoint.");
            return;
        }

        // Múltiplos valores no header é ambíguo — não há regra que diga qual
        // vence (proxy duplicou? cliente bugado?). Rejeitar 400 alinhado com
        // draft IETF que define o header como Item (sf-string), não List.
        if (headerValues.Count > 1)
        {
            ShortCircuit(context, IdempotencyDomainErrorCodes.KeyMalformada,
                "Header Idempotency-Key recebido múltiplas vezes; envie um único valor por request.");
            return;
        }

        string idempotencyKey = headerValues[0]!;
        if (!IsKeyValid(idempotencyKey))
        {
            ShortCircuit(context, IdempotencyDomainErrorCodes.KeyMalformada,
                "Header Idempotency-Key contém caracteres inválidos ou tamanho fora de [1, 255].");
            return;
        }

        // Resolve principal via IUserContext (lê claims em UM ÚNICO lugar —
        // ADR-0033). Endpoint marcado com [RequiresIdempotencyKey] em request
        // anonymous é inconsistência de configuração: caches anonymous
        // compartilhados permitem ataques de poisoning entre clientes.
        string? scope = ResolveScope(_userContext);
        if (scope is null)
        {
            ShortCircuit(context, IdempotencyDomainErrorCodes.PrincipalRequerido,
                "Endpoint com [RequiresIdempotencyKey] exige principal autenticado.");
            return;
        }

        // Lê body bytes (sem consumir o stream para o model binder).
        byte[] bodyBytes;
        try
        {
            bodyBytes = await ReadAndRewindBodyAsync(httpContext.Request, _options.MaxBodyBytes,
                httpContext.RequestAborted).ConfigureAwait(false);
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            // GlobalExceptionMiddleware do projeto trata Exception genérica como
            // 500. Capturar aqui e devolver 413 ProblemDetails canônico evita
            // que o cliente veja "internal error" para um problema de tamanho.
            ShortCircuit(context, IdempotencyDomainErrorCodes.BodyMuitoGrande,
                $"Request body excede o limite de {_options.MaxBodyBytes} bytes para endpoints idempotentes.");
            return;
        }

        string bodyHash = ComputeSha256Hex(bodyBytes);
        string endpoint = ResolveEndpoint(context);

        // Lookup inicial.
        IdempotencyLookupResult lookup = await _store.LookupAsync(
            scope, endpoint, idempotencyKey, bodyHash, httpContext.RequestAborted).ConfigureAwait(false);

        if (TryShortCircuitFromLookup(context, lookup, httpContext.RequestAborted, out Task? earlyReturn))
        {
            await earlyReturn!.ConfigureAwait(false);
            return;
        }

        // Tenta reservar. Se UNIQUE vencer, re-lookup: o vencedor da corrida
        // pode já ter completado (retornar HitMatch para replay), ainda estar
        // em Processing (409), ou ter Hit com body diferente (422). Sem o
        // re-lookup, perdedor receberia 409 mesmo se o vencedor já estivesse
        // pronto para replay — quebra de contrato draft IETF §3.
        DateTimeOffset expiresAt = _time.GetUtcNow().Add(_options.Ttl);
        bool reserved = await _store.TryReserveAsync(
            scope, endpoint, idempotencyKey, bodyHash, expiresAt, httpContext.RequestAborted).ConfigureAwait(false);

        if (!reserved)
        {
            IdempotencyLookupResult relookup = await _store.LookupAsync(
                scope, endpoint, idempotencyKey, bodyHash, httpContext.RequestAborted).ConfigureAwait(false);

            if (TryShortCircuitFromLookup(context, relookup, httpContext.RequestAborted, out Task? raceReturn))
            {
                await raceReturn!.ConfigureAwait(false);
                return;
            }

            // Miss aqui só ocorre se a entry foi DELETADA entre TryReserve e
            // re-lookup (5xx do vencedor) — situação rara, devolver 409 e
            // sugerir retry imediato é o caminho seguro.
            ShortCircuit(context, IdempotencyDomainErrorCodes.ProcessingConflict,
                "Request concorrente com mesma Idempotency-Key resolvida; tentar novamente.");
            return;
        }

        // Wrap response stream para captura. try/finally garante restauração
        // do body original e dispose do stream mesmo em caso de exceção do
        // handler — exceção também dispara delete da reservation (rethrow).
        Stream originalBody = httpContext.Response.Body;
        MemoryStream captureStream = new();
        httpContext.Response.Body = captureStream;
        bool reservationStillValid = true;
        bool handlerCompleted = false;

        try
        {
            ResourceExecutedContext executed = await next().ConfigureAwait(false);
            handlerCompleted = true;

            int status = httpContext.Response.StatusCode;

            // 5xx nunca é cacheado (ADR-0027 §"Status codes em cache" + draft
            // IETF §6). Reservation deletada para que retry do cliente com a
            // mesma key possa executar o handler novamente — sem isso, cliente
            // ficaria preso em 409 ProcessingConflict por 24h. CancellationToken
            // None no DeleteAsync porque RequestAborted já está disparado quando
            // o request foi cancelado pelo cliente.
            if (status >= 500 || executed.Canceled)
            {
                await _store.DeleteAsync(scope, endpoint, idempotencyKey, CancellationToken.None)
                    .ConfigureAwait(false);
                reservationStillValid = false;
                captureStream.Position = 0;
                await captureStream.CopyToAsync(originalBody, httpContext.RequestAborted).ConfigureAwait(false);
                return;
            }

            byte[] responseBytes = captureStream.ToArray();

            // Cifra response body at-rest (ADR-0027 §"Cifragem at-rest").
            byte[] cipher = await _encryption.EncryptAsync(
                IdempotencyOptions.EncryptionKeyName, responseBytes, httpContext.RequestAborted).ConfigureAwait(false);

            // Captura headers cacheáveis.
            string headersJson = SerializeCachedHeaders(httpContext.Response);

            await _store.CompleteAsync(
                scope, endpoint, idempotencyKey, status, headersJson, cipher,
                httpContext.RequestAborted).ConfigureAwait(false);
            reservationStillValid = false;

            // Copia bytes capturados para o stream original (cliente recebe normal).
            captureStream.Position = 0;
            await captureStream.CopyToAsync(originalBody, httpContext.RequestAborted).ConfigureAwait(false);
        }
        catch
        {
            // Discrimina entre falha PRÉ-handler (rollback semanticamente seguro
            // — handler nem completou, retry pode rodar de novo) e falha
            // PÓS-handler (Encrypt/Complete falharam DEPOIS do handler já ter
            // commitado o agregado — deletar reservation aqui permitiria que
            // retry do cliente recriasse o agregado, causando duplicação
            // semântica como dois EditalPublicado para o mesmo edital).
            //
            // Política conservadora: só limpa reservation se handler não
            // completou. Se handler já rodou, reservation fica em Processing
            // até TTL → cliente recebe 409 nesse intervalo (ADR-0027 §
            // "Atomicidade parcial" reconhece essa janela).
            if (reservationStillValid && !handlerCompleted)
            {
                try
                {
                    await _store.DeleteAsync(scope, endpoint, idempotencyKey, CancellationToken.None)
                        .ConfigureAwait(false);
                }
#pragma warning disable CA1031 // catch genérico justificado: delete é best-effort no rollback path; falha aqui deve cair no TTL, não substituir/mascarar a exceção original que estamos rethrowing.
                catch
                {
                    // Best-effort: se delete falhar, entry expira via TTL.
                }
#pragma warning restore CA1031
            }
            throw;
        }
        finally
        {
            httpContext.Response.Body = originalBody;
            await captureStream.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Traduz <see cref="IdempotencyLookupResult"/> em short-circuit do filter.
    /// Retorna <c>true</c> + Task a aguardar quando o resultado é terminal
    /// (HitMatch/HitMismatch/Processing); <c>false</c> + Task null quando é
    /// Miss (caller continua para TryReserve).
    /// </summary>
    private bool TryShortCircuitFromLookup(
        ResourceExecutingContext context,
        IdempotencyLookupResult lookup,
        CancellationToken cancellationToken,
        out Task? earlyReturn)
    {
        switch (lookup.Outcome)
        {
            case IdempotencyOutcome.HitMatch:
                earlyReturn = ReplayAsync(context, lookup.Entry!, cancellationToken);
                return true;

            case IdempotencyOutcome.HitMismatch:
                ShortCircuit(context, IdempotencyDomainErrorCodes.BodyMismatch,
                    "Mesma Idempotency-Key reusada com body diferente.");
                earlyReturn = Task.CompletedTask;
                return true;

            case IdempotencyOutcome.Processing:
                ShortCircuit(context, IdempotencyDomainErrorCodes.ProcessingConflict,
                    "Request com a mesma Idempotency-Key ainda em processamento; tentar novamente.");
                earlyReturn = Task.CompletedTask;
                return true;

            case IdempotencyOutcome.Miss:
            default:
                earlyReturn = null;
                return false;
        }
    }

    private static bool HasRequiresIdempotencyKey(ResourceExecutingContext context)
    {
        if (context.ActionDescriptor is not ControllerActionDescriptor descriptor)
            return false;

        // Atributo aceita AttributeTargets.Method | Class. Method-level vence
        // (controlle granularidade fina); class-level cobre o caso "todos os
        // POSTs deste controller exigem Idempotency-Key" sem repetir o atributo.
        return descriptor.MethodInfo.GetCustomAttribute<RequiresIdempotencyKeyAttribute>(inherit: true) is not null
            || descriptor.ControllerTypeInfo.GetCustomAttribute<RequiresIdempotencyKeyAttribute>(inherit: true) is not null;
    }

    private static bool IsKeyValid(string key)
    {
        // Regex {1,255} já garante length em range e [\x21-\x7E] já exclui
        // espaço (0x20), tab (0x09) e demais caracteres não-imprimíveis.
        // ForbiddenKeyChars cobre apenas vírgula (0x2C) e ponto-vírgula (0x3B)
        // que estão dentro do range printable mas são proibidos pelo
        // draft IETF (caracteres usados como separadores em sf-list).
        if (key.IndexOfAny(ForbiddenKeyChars) >= 0)
            return false;

        return KeyPrintableAsciiRegex().IsMatch(key);
    }

    private static async Task<byte[]> ReadAndRewindBodyAsync(
        HttpRequest request, long maxBytes, CancellationToken cancellationToken)
    {
        request.EnableBuffering();
        request.Body.Position = 0;

        long contentLength = request.ContentLength ?? -1;
        if (contentLength == 0)
        {
            request.Body.Position = 0;
            return [];
        }

        if (contentLength > maxBytes)
            throw new BadHttpRequestException(
                $"Request body excede o limite de {maxBytes} bytes para endpoints idempotentes.",
                StatusCodes.Status413PayloadTooLarge);

        // Leitura em chunks com early break: cliente malicioso enviando
        // chunked transfer encoding sem Content-Length não causa OOM.
        // ArrayPool evita alocação por request.
        const int chunkSize = 81920;
        byte[] rented = ArrayPool<byte>.Shared.Rent(chunkSize);
        try
        {
            using MemoryStream buffer = new();
            int bytesRead;
            while ((bytesRead = await request.Body.ReadAsync(rented.AsMemory(0, chunkSize), cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (buffer.Length + bytesRead > maxBytes)
                {
                    request.Body.Position = 0;
                    throw new BadHttpRequestException(
                        $"Request body excede o limite de {maxBytes} bytes para endpoints idempotentes.",
                        StatusCodes.Status413PayloadTooLarge);
                }
                await buffer.WriteAsync(rented.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
            }
            request.Body.Position = 0;
            return buffer.ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static string ComputeSha256Hex(byte[] bytes)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(bytes, hash);
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Resolve o scope da chave de cache via <see cref="IUserContext"/>
    /// (single source of truth pra claims, ADR-0033). Retorna <c>null</c>
    /// quando não há principal autenticado — sinal para rejeitar o request
    /// com 401, evitando que clientes anonymous compartilhem o mesmo bucket
    /// e poluam/colidam keys uns dos outros (cache poisoning).
    /// </summary>
    private static string? ResolveScope(IUserContext userContext)
    {
        if (!userContext.IsAuthenticated || string.IsNullOrEmpty(userContext.UserId))
            return null;

        return $"user:{userContext.UserId}";
    }

    private static string ResolveEndpoint(ResourceExecutingContext context)
    {
        // Path concreto (não template) — `/api/editais/abc/publicar` e
        // `/api/editais/def/publicar` PRECISAM ser keys distintas no cache;
        // o template colapsaria ambos em "POST /api/editais/{id}/publicar"
        // e duas requests com mesma Idempotency-Key sobre RECURSOS DIFERENTES
        // colidiriam (vazamento cross-resource).
        //
        // Casing canonicalizado: ASP.NET Core route matching é case-insensitive
        // por default, então `/api/Editais/abc/Publicar` e
        // `/api/editais/abc/publicar` matcham a mesma action. Sem normalizar,
        // cliente que alterne casing acidentalmente (frontend com URL diferente)
        // teria caches separados — replay quebrado. Lowercase é prática
        // canônica de URL normalization (RFC 3986 §6.2.2.1).
        string method = context.HttpContext.Request.Method;
#pragma warning disable CA1308 // ToLowerInvariant em URL é canônico — ToUpperInvariant deixaria a key ilegível em queries SQL diagnósticas.
        string path = context.HttpContext.Request.Path.ToString().ToLowerInvariant();
#pragma warning restore CA1308
        return $"{method} {path}";
    }

    private void ShortCircuit(ResourceExecutingContext context, string domainCode, string detail)
    {
        DomainError error = new(domainCode, detail);
        context.Result = Result.Failure(error).ToActionResult(_errorMapper);
    }

    private async Task ReplayAsync(ResourceExecutingContext context, IdempotencyEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry.ResponseBodyCipher);

        byte[] body = await _encryption.DecryptAsync(
            IdempotencyOptions.EncryptionKeyName, entry.ResponseBodyCipher, cancellationToken).ConfigureAwait(false);

        Dictionary<string, string>? headers = string.IsNullOrEmpty(entry.ResponseHeadersJson)
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, string>>(entry.ResponseHeadersJson);

        string? cachedContentType = headers?.GetValueOrDefault("Content-Type");
        int statusCode = entry.ResponseStatus ?? StatusCodes.Status200OK;

        // 204 NoContent / response sem body+content-type cacheado: replay com
        // StatusCodeResult preserva semântica HTTP (sem Content-Type, sem body).
        // Forçar FileContentResult com "application/json" default em 204
        // violaria RFC 9110 §6.4.5 (NoContent não pode ter Content-Type).
        if (body.Length == 0 && cachedContentType is null)
        {
            context.Result = new StatusCodeResult(statusCode);
        }
        else
        {
            // FileContentResult devolve bytes verbatim (sem decoding UTF-8) —
            // funciona para JSON e para qualquer Content-Type binário futuro
            // (PDF, octet-stream) sem corromper o body.
            FileContentResult result = new(body, cachedContentType ?? "application/json")
            {
                FileDownloadName = null,
            };
            context.Result = result;
            context.HttpContext.Response.StatusCode = statusCode;
        }

        // Replay header — não normativo (draft IETF não exige) mas útil para
        // clientes diagnosticarem retry vs. primeira execução.
        context.HttpContext.Response.Headers[ReplayHeader] = "true";

        // Restaura Location se o response original carregava (POST que gerou
        // recurso novo).
        if (headers is not null && headers.TryGetValue("Location", out string? loc))
            context.HttpContext.Response.Headers.Location = loc;
    }

    private static string SerializeCachedHeaders(HttpResponse response)
    {
        // Cacheamos apenas Content-Type e Location (ADR-0027 §"Esta ADR não decide"
        // permite expansão futura). Outros headers são sensíveis a contexto
        // (Set-Cookie, ETag dinâmico, traceId).
        Dictionary<string, string> cached = new(StringComparer.OrdinalIgnoreCase);

        if (response.ContentType is { Length: > 0 } ct)
            cached["Content-Type"] = ct;

        if (response.Headers.TryGetValue("Location", out Microsoft.Extensions.Primitives.StringValues loc) && loc.Count > 0 && !string.IsNullOrEmpty(loc[0]))
            cached["Location"] = loc[0]!;

        return JsonSerializer.Serialize(cached);
    }
}
