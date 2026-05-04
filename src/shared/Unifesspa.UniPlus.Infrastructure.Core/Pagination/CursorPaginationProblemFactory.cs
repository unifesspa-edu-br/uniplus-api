namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;

using Errors;

using Kernel.Results;

/// <summary>
/// Constrói a resposta para falhas de model binding de cursor pagination,
/// chamada por <c>ApiBehaviorOptions.InvalidModelStateResponseFactory</c>.
/// Lê o código de domínio publicado pelo <see cref="PageRequestModelBinder"/>
/// em <see cref="CursorBindingErrorCodes.HttpContextItemKey"/>, traduz para
/// HTTP status via <see cref="IDomainErrorMapper"/> e produz
/// <see cref="ProblemDetails"/> conforme RFC 9457 (ADR-0023).
/// <para>
/// Quando o erro não é de cursor binding, devolve <c>null</c> para o caller
/// aplicar o factory default (validação MVC convencional).
/// </para>
/// </summary>
public static class CursorPaginationProblemFactory
{
    public static IActionResult? TryBuild(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.HttpContext.Items.TryGetValue(CursorBindingErrorCodes.HttpContextItemKey, out object? value)
            || value is not string domainCode)
        {
            return null;
        }

        IDomainErrorMapper mapper = context.HttpContext.RequestServices.GetRequiredService<IDomainErrorMapper>();
        string detail = ExtractDetail(context.ModelState) ?? "Falha no parsing do cursor.";

        DomainError error = new(domainCode, detail);
        return Result.Failure(error).ToActionResult(mapper);
    }

    private static string? ExtractDetail(ModelStateDictionary modelState)
    {
        foreach (ModelStateEntry entry in modelState.Values)
        {
            foreach (ModelError modelError in entry.Errors)
            {
                if (!string.IsNullOrEmpty(modelError.ErrorMessage))
                    return modelError.ErrorMessage;
            }
        }

        return null;
    }
}
