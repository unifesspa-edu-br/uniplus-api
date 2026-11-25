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

    public static IActionResult ToCreatedResult(
        this Result<Guid> resultado,
        ControllerBase controlador,
        string acaoDeLeitura,
        IDomainErrorMapper mapeador)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        ArgumentNullException.ThrowIfNull(controlador);
        ArgumentNullException.ThrowIfNull(mapeador);

        return resultado.IsSuccess
            ? controlador.CreatedAtAction(acaoDeLeitura, new { id = resultado.Value }, resultado.Value)
            : resultado.ToActionResult(mapeador);
    }

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

    /// <summary>
    /// Devolve <c>null</c> quando o Id da rota é o mesmo do corpo; senão, a recusa registrada na
    /// fronteira (400). Igual para todo endpoint de atualização.
    /// </summary>
    public static IActionResult? ConferirIdDaRota(this ControllerBase controller, Guid idDaRota, Guid idDoCorpo, IDomainErrorMapper mapper)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(mapper);

        return idDaRota == idDoCorpo
            ? null
            : Result.Failure(new DomainError(
                    ErrosDaFronteiraHttp.IdDaRotaDivergenteDoCorpo.Code,
                    "O Id informado na rota é diferente do Id do corpo da requisição; os dois precisam identificar o mesmo recurso."))
                .ToActionResult(mapper);
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
    /// Todas as causas quando há várias ou alguma aponta um campo; caso contrário, nenhuma coleção.
    /// </summary>
    /// <remarks>
    /// A primeira causa define a resposta. A coleção preserva as demais causas independentes e uma
    /// causa única com campo, inclusive quando o campo vazio aponta a raiz do documento (ADR-0023).
    /// Uma única causa sem campo já está descrita na raiz. Cada item sem campo omite a chave field.
    /// </remarks>
    private static object[]? ErrosDeCampo(IReadOnlyList<FieldError> errors, IDomainErrorMapper mapper) =>
        errors.Count > 1 || errors.Any(fieldError => fieldError.Field is not null)
            ? [.. errors.Select(fieldError => ItemDeErroDeCampo.De(fieldError, mapper))]
            : null;
}
