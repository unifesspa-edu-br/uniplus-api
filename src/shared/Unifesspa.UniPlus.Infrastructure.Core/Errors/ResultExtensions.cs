namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

using Kernel.Results;

using Microsoft.AspNetCore.Mvc;

[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "Extension method class precisa ser public para ser acessível nos projetos API que referenciam Infrastructure.Core.")]
public static class ResultExtensions
{
    /// <summary>
    /// Nome, no envelope RFC 9457, da extensão que declara o conflito cuja repetição pode ser
    /// aceita. Quem a escreve é este tipo; quem a lê para decidir se a chave de idempotência
    /// fica ocupada é o filtro de idempotência; e o schema `ProblemDetails` dos contratos
    /// publicados a declara. Um nome só para os três, porque uma renomeação que alcançasse
    /// apenas um deles quebraria em silêncio (ADR-0134).
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

    private static ObjectResult BuildProblemDetailsResult(IReadOnlyList<FieldError> errors, IDomainErrorMapper mapper)
    {
        // Fail-fast: a primeira violação determina status/type/title/code da raiz —
        // mesma semântica que o domínio já tinha antes de acumular (ADR-0125).
        (int status, string type, string title, string code, bool retryableConflict) =
            DomainErrorProblemDetailsFactory.Resolve(errors[0].Error, mapper);

        ProblemDetails problem = new()
        {
            Status = status,
            Type = type,
            Title = title,
            // Invariante: DomainError.Message não deve conter PII (CPF, e-mail, nome).
            // O linter AssertNoPiiAsync detecta violações nos testes de integração.
            Detail = errors[0].Error.Message,
            Instance = $"urn:uuid:{Guid.CreateVersion7()}",
        };

        problem.Extensions["code"] = code;

        // Só quando verdadeiro: um campo presente em toda resposta de erro seria ruído, e a
        // ausência já diz "não conte com repetir". Declarado aqui, e não deduzido pelo cliente a
        // partir do status, porque 409 sozinho não separa a corrida que já passou do estado que
        // permanece — e é o produtor do erro, não quem o recebe, que sabe qual dos dois é.
        if (retryableConflict)
        {
            problem.Extensions[RetryableExtensionName] = true;
        }

        problem.Extensions["traceId"] = Activity.Current?.TraceId.ToHexString()
            ?? Guid.CreateVersion7().ToString("N");

        // errors[] é extension condicional a erro DE CAMPO (ADR-0023) — não a status
        // 422 isolado nem a ter mais de uma violação. Um Result.Failure comum
        // (ex.: CampusResponsavelNaoEncontrado, Snapshot.VigenteAusente) também
        // resolve para 422 por regra de negócio sem ser validação de campo, e vira
        // FieldError(null, error) só pela forma interna do Result — não tem
        // "field" nenhum para reportar, então não pode aparecer no array com
        // field: null. O sinal correto é a presença de Field, que só existe
        // quando o Result veio de ValidationFailure.
        if (errors.Any(fieldError => fieldError.Field is not null))
        {
            problem.Extensions["errors"] = errors
                .Select(fieldError =>
                {
                    (int _, string _, string _, string fieldCode, bool _) = DomainErrorProblemDetailsFactory.Resolve(fieldError.Error, mapper);
                    return new { field = fieldError.Field, code = fieldCode, message = fieldError.Error.Message };
                })
                .ToArray();
        }

        return new ObjectResult(problem) { StatusCode = status };
    }
}
