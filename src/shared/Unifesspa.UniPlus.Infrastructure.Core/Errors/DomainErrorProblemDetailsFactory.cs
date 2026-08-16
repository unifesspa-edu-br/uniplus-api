namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Traduz um <see cref="DomainError"/> isolado para os campos <c>status</c>/<c>type</c>/
/// <c>title</c>/<c>code</c> RFC 9457 (ADR-0023/ADR-0024) — o mesmo trio que
/// <c>IDomainErrorMapper</c> resolve, sem repetir a lógica de fallback ("código não
/// mapeado" → 400/<c>uniplus.erro_nao_mapeado</c>) em cada lugar que precisa dela.
/// </summary>
public static class DomainErrorProblemDetailsFactory
{
    public static (int Status, string Type, string Title, string Code) Resolve(DomainError error, IDomainErrorMapper mapper)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(mapper);

        bool found = mapper.TryGetMapping(error.Code, out DomainErrorMapping? mapping);

        int status = found ? mapping!.Status : StatusCodes.Status400BadRequest;
        string code = found ? mapping!.Code : "uniplus.erro_nao_mapeado";
        string title = found ? mapping!.Title : "Erro de domínio";

        return (status, mapper.GetProblemTypeUri(code), title, code);
    }
}
