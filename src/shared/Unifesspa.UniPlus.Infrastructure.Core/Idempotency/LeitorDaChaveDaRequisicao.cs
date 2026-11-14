namespace Unifesspa.UniPlus.Infrastructure.Core.Idempotency;

using Kernel.Results;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>
/// Lê da requisição a <see cref="ChaveDaRequisicao"/>: o header <c>Idempotency-Key</c> conforme o draft IETF, o principal
/// autenticado e o corpo. Devolve o erro de domínio que o filtro responde quando algum deles não serve.
/// </summary>
internal static class LeitorDaChaveDaRequisicao
{
    private const string NomeDoHeader = "Idempotency-Key";

    public static async Task<Result<ChaveDaRequisicao>> LerAsync(HttpContext http, IUserContext usuario, long limiteDoCorpo)
    {
        Result<string> chave = LerChaveExigindoPrincipal(http.Request.Headers, usuario);
        if (chave.IsFailure)
        {
            return Result<ChaveDaRequisicao>.Failure(chave.Error!);
        }

        Result<string> resumo = await ResumirOCorpoAsync(http, limiteDoCorpo).ConfigureAwait(false);
        return resumo.IsFailure
            ? Result<ChaveDaRequisicao>.Failure(resumo.Error!)
            : Result<ChaveDaRequisicao>.Success(new ChaveDaRequisicao($"user:{usuario.UserId}", EndpointDe(http.Request), chave.Value!, resumo.Value!));
    }

    private static Result<string> LerChaveExigindoPrincipal(IHeaderDictionary headers, IUserContext usuario)
    {
        Result<string> chave = LerChave(headers);

        // Endpoint idempotente em request anônimo é inconsistência de configuração: um cache anônimo compartilhado
        // permitiria envenenar a chave de outro cliente.
        return chave.IsSuccess && (!usuario.IsAuthenticated || string.IsNullOrEmpty(usuario.UserId))
            ? Falha<string>(IdempotencyDomainErrorCodes.PrincipalRequerido, "Endpoint com [RequiresIdempotencyKey] exige principal autenticado.")
            : chave;
    }

    private static Result<string> LerChave(IHeaderDictionary headers)
    {
        if (!headers.TryGetValue(NomeDoHeader, out StringValues valores) || valores.Count == 0 || string.IsNullOrEmpty(valores[0]))
        {
            return Falha<string>(IdempotencyDomainErrorCodes.KeyAusente, "Header Idempotency-Key é obrigatório neste endpoint.");
        }

        // Mais de um valor é ambíguo, e nenhuma regra diz qual vence: o draft define o header como Item (sf-string), não List.
        if (valores.Count > 1)
        {
            return Falha<string>(IdempotencyDomainErrorCodes.KeyMalformada, "Header Idempotency-Key recebido múltiplas vezes; envie um único valor por request.");
        }

        return IdempotencyKeyValidator.IsValid(valores[0]!)
            ? Result<string>.Success(valores[0]!)
            : Falha<string>(IdempotencyDomainErrorCodes.KeyMalformada, "Header Idempotency-Key contém caracteres inválidos ou tamanho fora de [1, 255].");
    }

    private static async Task<Result<string>> ResumirOCorpoAsync(HttpContext http, long limite)
    {
        try
        {
            byte[] corpo = await CorpoDaRequisicao.LerSemConsumirAsync(http.Request, limite, http.RequestAborted).ConfigureAwait(false);
            return Result<string>.Success(CorpoDaRequisicao.ResumoSha256(corpo));
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            // O middleware global trata exceção genérica como 500: devolver o 413 canônico evita o "erro interno" para
            // um problema de tamanho.
            return Falha<string>(IdempotencyDomainErrorCodes.BodyMuitoGrande, $"Request body excede o limite de {limite} bytes para endpoints idempotentes.");
        }
    }

    // O path concreto, e não o template: duas requisições com a mesma chave sobre recursos diferentes precisam de entradas
    // distintas, ou colidiriam entre recursos. Em minúsculas, porque o roteamento não diferencia caixa e a mesma action
    // não pode ter caches separados (RFC 3986 §6.2.2.1).
    private static string EndpointDe(HttpRequest request) =>
        $"{request.Method} {request.Path.ToString().ToLowerInvariant()}";

    private static Result<T> Falha<T>(string codigo, string detalhe) =>
        Result<T>.Failure(new DomainError(codigo, detalhe));
}
