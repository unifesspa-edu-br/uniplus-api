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

    private static ObjectResult BuildProblemDetailsResult(IReadOnlyList<FieldError> errors, IDomainErrorMapper mapper)
    {
        DomainError primeira = errors[0].Error;
        ProblemDetails problem = EnvelopeDaPrimeiraViolacao(primeira, mapper);

        if (ErrosDeCampo(errors, mapper) is { } errosDeCampo)
        {
            problem.Extensions["errors"] = errosDeCampo;
        }

        return mapper.TryGetMapping(primeira.Code, out DomainErrorMapping? mapping) && mapping.RecusaAposGravacao
            ? new RespostaDeRecusaAposGravacao(problem) { StatusCode = problem.Status }
            : new ObjectResult(problem) { StatusCode = problem.Status };
    }

    /// <summary>
    /// Fail-fast: a primeira violação determina status, type, title e code da raiz, a mesma
    /// semântica que o domínio já tinha antes de acumular (ADR-0125, ADR-0024).
    /// </summary>
    /// <remarks>
    /// Invariante: <c>DomainError.Message</c> não deve conter PII (CPF, e-mail, nome). O linter
    /// <c>AssertNoPiiAsync</c> detecta violações nos testes de integração.
    /// </remarks>
    private static ProblemDetails EnvelopeDaPrimeiraViolacao(DomainError primeira, IDomainErrorMapper mapper)
    {
        (int status, string type, string title, string code, bool retryableConflict) =
            DomainErrorProblemDetailsFactory.Resolve(primeira, mapper);

        ProblemDetails problem = new()
        {
            Status = status,
            Type = type,
            Title = title,
            Detail = primeira.Message,
            Instance = $"urn:uuid:{Guid.CreateVersion7()}",
        };

        EscreverExtensoesDaRaiz(problem.Extensions, code, retryableConflict);
        return problem;
    }

    /// <remarks>
    /// O <c>retryable</c> só sai quando verdadeiro: um campo presente em toda resposta de erro seria
    /// ruído, e a ausência já diz "não conte com repetir". Declarado aqui, e não deduzido pelo
    /// cliente a partir do status, porque 409 sozinho não separa a corrida que já passou do estado
    /// que permanece — e é o produtor do erro, não quem o recebe, que sabe qual dos dois é.
    /// </remarks>
    private static void EscreverExtensoesDaRaiz(IDictionary<string, object?> extensoes, string code, bool retryableConflict)
    {
        extensoes["code"] = code;

        if (retryableConflict)
        {
            extensoes[RetryableExtensionName] = true;
        }

        extensoes["traceId"] = Activity.Current?.TraceId.ToHexString()
            ?? Guid.CreateVersion7().ToString("N");
    }

    /// <summary>
    /// Os erros por campo (<c>field</c>, <c>code</c>, <c>message</c>), ou <c>null</c> quando nenhuma
    /// violação tem campo.
    /// </summary>
    /// <remarks>
    /// O <c>errors[]</c> é condicional a erro DE CAMPO (ADR-0023), não a status 422 isolado nem a ter
    /// mais de uma violação. Um <c>Result.Failure</c> comum (ex.: CampusResponsavelNaoEncontrado,
    /// Snapshot.VigenteAusente) também resolve para 422 por regra de negócio sem ser validação de
    /// campo, e vira <c>FieldError(null, error)</c> só pela forma interna do <c>Result</c>: não tem
    /// campo para reportar, então não pode aparecer no array com <c>field: null</c>. O sinal correto
    /// é a presença de <c>Field</c>, que só existe quando o <c>Result</c> veio de
    /// <c>ValidationFailure</c>.
    /// </remarks>
    private static object[]? ErrosDeCampo(IReadOnlyList<FieldError> errors, IDomainErrorMapper mapper) =>
        errors.Any(fieldError => fieldError.Field is not null)
            ? [.. errors.Select(fieldError => new
            {
                field = fieldError.Field,
                code = DomainErrorProblemDetailsFactory.Resolve(fieldError.Error, mapper).Code,
                message = fieldError.Error.Message,
            })]
            : null;
}
