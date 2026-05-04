namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Kernel.Results;

[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "Extension method class precisa ser public para ser acessível nos projetos API que referenciam Infrastructure.Core.")]
public static class ResultExtensions
{
    public static IActionResult ToActionResult<T>(this Result<T> result, IDomainErrorMapper mapper)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(mapper);
        if (result.IsSuccess)
            throw new InvalidOperationException("ToActionResult deve ser chamado apenas em caso de falha.");
        return BuildProblemDetailsResult(result.Error!, mapper);
    }

    public static IActionResult ToActionResult(this Result result, IDomainErrorMapper mapper)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(mapper);
        if (result.IsSuccess)
            throw new InvalidOperationException("ToActionResult deve ser chamado apenas em caso de falha.");
        return BuildProblemDetailsResult(result.Error!, mapper);
    }

    private static ObjectResult BuildProblemDetailsResult(DomainError error, IDomainErrorMapper mapper)
    {
        bool found = mapper.TryGetMapping(error.Code, out DomainErrorMapping? mapping);

        int status = found ? mapping!.Status : StatusCodes.Status400BadRequest;
        string code = found ? mapping!.Code : "uniplus.erro_nao_mapeado";
        string title = found ? mapping!.Title : "Erro de domínio";

        ProblemDetails problem = new()
        {
            Status = status,
            Type = ProblemDetailsConstants.ErrorsBaseUri + code,
            Title = title,
            // Invariante: error.Message não deve conter PII (CPF, e-mail, nome).
            // O linter AssertNoPiiAsync detecta violações nos testes de integração.
            Detail = error.Message,
            Instance = $"urn:uuid:{Guid.CreateVersion7()}",
        };

        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = Activity.Current?.TraceId.ToHexString()
            ?? Guid.CreateVersion7().ToString("N");

        return new ObjectResult(problem) { StatusCode = status };
    }
}
