namespace Unifesspa.UniPlus.Infrastructure.Core.Idempotency;

using Kernel.Results;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>
/// Lê da requisição a <see cref="IdempotencyRequestKey"/>: o header <c>Idempotency-Key</c> conforme o draft IETF, o principal
/// autenticado e o corpo. Devolve o erro de domínio que o filtro responde quando algum deles não serve.
/// </summary>
internal static class IdempotencyRequestKeyReader
{
    private const string HeaderName = "Idempotency-Key";

    public static async Task<Result<IdempotencyRequestKey>> ReadAsync(HttpContext http, IUserContext user, long bodyLimit)
    {
        Result<string> key = ReadKeyRequiringPrincipal(http.Request.Headers, user);
        if (key.IsFailure)
        {
            return Result<IdempotencyRequestKey>.Failure(key.Error!);
        }

        Result<string> bodyHash = await HashBodyAsync(http, bodyLimit).ConfigureAwait(false);
        return bodyHash.IsFailure
            ? Result<IdempotencyRequestKey>.Failure(bodyHash.Error!)
            : Result<IdempotencyRequestKey>.Success(new IdempotencyRequestKey($"user:{user.UserId}", EndpointOf(http.Request), key.Value!, bodyHash.Value!));
    }

    private static Result<string> ReadKeyRequiringPrincipal(IHeaderDictionary headers, IUserContext user)
    {
        Result<string> key = ReadKey(headers);

        // Endpoint idempotente em request anônimo é inconsistência de configuração: um cache anônimo compartilhado
        // permitiria envenenar a chave de outro cliente.
        return key.IsSuccess && (!user.IsAuthenticated || string.IsNullOrEmpty(user.UserId))
            ? Failure<string>(IdempotencyDomainErrorCodes.PrincipalRequerido, "Endpoint com [RequiresIdempotencyKey] exige principal autenticado.")
            : key;
    }

    private static Result<string> ReadKey(IHeaderDictionary headers)
    {
        if (!headers.TryGetValue(HeaderName, out StringValues values) || values.Count == 0 || string.IsNullOrEmpty(values[0]))
        {
            return Failure<string>(IdempotencyDomainErrorCodes.KeyAusente, "Header Idempotency-Key é obrigatório neste endpoint.");
        }

        // Mais de um valor é ambíguo, e nenhuma regra diz qual vence: o draft define o header como Item (sf-string), não List.
        if (values.Count > 1)
        {
            return Failure<string>(IdempotencyDomainErrorCodes.KeyMalformada, "Header Idempotency-Key recebido múltiplas vezes; envie um único valor por request.");
        }

        return IdempotencyKeyValidator.IsValid(values[0]!)
            ? Result<string>.Success(values[0]!)
            : Failure<string>(IdempotencyDomainErrorCodes.KeyMalformada, "Header Idempotency-Key contém caracteres inválidos ou tamanho fora de [1, 255].");
    }

    private static async Task<Result<string>> HashBodyAsync(HttpContext http, long limit)
    {
        try
        {
            byte[] body = await RequestBodyReader.ReadWithoutConsumingAsync(http.Request, limit, http.RequestAborted).ConfigureAwait(false);
            return Result<string>.Success(RequestBodyReader.Sha256Hex(body));
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            // O middleware global trata exceção genérica como 500: devolver o 413 canônico evita o "erro interno" para
            // um problema de tamanho.
            return Failure<string>(IdempotencyDomainErrorCodes.BodyMuitoGrande, $"Request body excede o limite de {limit} bytes para endpoints idempotentes.");
        }
    }

    // O path concreto, e não o template: duas requisições com a mesma chave sobre recursos diferentes precisam de entradas
    // distintas, ou colidiriam entre recursos. Em minúsculas, porque o roteamento não diferencia caixa e a mesma action
    // não pode ter caches separados (RFC 3986 §6.2.2.1).
    private static string EndpointOf(HttpRequest request) =>
        $"{request.Method} {request.Path.ToString().ToLowerInvariant()}";

    private static Result<T> Failure<T>(string code, string detail) =>
        Result<T>.Failure(new DomainError(code, detail));
}
