namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

using System.Diagnostics;
using System.Linq;

using Kernel.Results;

using Microsoft.AspNetCore.Mvc;

public static class ResultExtensions
{
    /// <summary>
    /// Nome, no envelope RFC 9457, da extensão que declara o conflito cuja repetição pode ser
    /// aceita. Quem a escreve é este tipo, e o schema `ProblemDetails` dos contratos publicados a
    /// declara. É um sinal ao cliente, não uma decisão do filtro de idempotência. Um nome só
    /// para os dois, porque uma renomeação que alcançasse apenas um deles quebraria em silêncio
    /// (ADR-0134).
    /// </summary>
    public const string RetryableExtensionName = "retryable";

    public static IActionResult ToActionResult<T>(this Result<T> result, IDomainErrorMapper mapper)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(mapper);
        if (result.IsSuccess)
            throw new InvalidOperationException("ToActionResult deve ser chamado apenas em caso de falha.");
        return BuildProblemDetailsResult(result.Errors, mapper);
    }

    public static IActionResult ToActionResult(this Result result, IDomainErrorMapper mapper)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(mapper);
        if (result.IsSuccess)
            throw new InvalidOperationException("ToActionResult deve ser chamado apenas em caso de falha.");
        return BuildProblemDetailsResult(result.Errors, mapper);
    }

    // Fail-fast: a primeira violação determina status/type/title/code da raiz —
    // mesma semântica que o domínio já tinha antes de acumular (ADR-0125).
    private static ObjectResult BuildProblemDetailsResult(IReadOnlyList<FieldError> errors, IDomainErrorMapper mapper)
    {
        (int status, string type, string title, string code, bool retryableConflict) =
            DomainErrorProblemDetailsFactory.Resolve(errors[0].Error, mapper);

        ProblemDetails problem = NovoProblema(errors[0].Error, status, type, title);
        problem.Extensions["code"] = code;
        DeclararConflitoRetentavel(problem, retryableConflict);
        problem.Extensions["traceId"] = Activity.Current?.TraceId.ToHexString()
            ?? Guid.CreateVersion7().ToString("N");
        DeclararErrosDeCampo(problem, errors, mapper);

        return mapper.TryGetMapping(errors[0].Error.Code, out DomainErrorMapping? mapping) && mapping.RecusaAposGravacao
            ? new RespostaDeRecusaAposGravacao(problem) { StatusCode = status }
            : new ObjectResult(problem) { StatusCode = status };
    }

    private static ProblemDetails NovoProblema(DomainError primeiroErro, int status, string type, string title) => new()
    {
        Status = status,
        Type = type,
        Title = title,
        // Invariante: DomainError.Message não deve conter PII (CPF, e-mail, nome).
        // O linter AssertNoPiiAsync detecta violações nos testes de integração.
        Detail = primeiroErro.Message,
        Instance = $"urn:uuid:{Guid.CreateVersion7()}",
    };

    // Só quando verdadeiro: um campo presente em toda resposta de erro seria ruído, e a
    // ausência já diz "não conte com repetir". Declarado aqui, e não deduzido pelo cliente a
    // partir do status, porque 409 sozinho não separa a corrida que já passou do estado que
    // permanece — e é o produtor do erro, não quem o recebe, que sabe qual dos dois é.
    private static void DeclararConflitoRetentavel(ProblemDetails problem, bool retryableConflict)
    {
        if (retryableConflict)
        {
            problem.Extensions[RetryableExtensionName] = true;
        }
    }

    // errors[] é extension condicional a erro DE CAMPO (ADR-0023) — não a status
    // 422 isolado nem a ter mais de uma violação. Um Result.Failure comum
    // (ex.: CampusResponsavelNaoEncontrado, Snapshot.VigenteAusente) também
    // resolve para 422 por regra de negócio sem ser validação de campo, e vira
    // FieldError(null, error) só pela forma interna do Result — não tem
    // "field" nenhum para reportar, então não pode aparecer no array com
    // field: null. O sinal correto é a presença de Field, que só existe
    // quando o Result veio de ValidationFailure.
    private static void DeclararErrosDeCampo(ProblemDetails problem, IReadOnlyList<FieldError> errors, IDomainErrorMapper mapper)
    {
        if (errors.Any(fieldError => fieldError.Field is not null))
        {
            problem.Extensions["errors"] = errors
                .Select(fieldError => ItemDeErroDeCampo.De(fieldError, mapper))
                .ToArray();
        }
    }
}
