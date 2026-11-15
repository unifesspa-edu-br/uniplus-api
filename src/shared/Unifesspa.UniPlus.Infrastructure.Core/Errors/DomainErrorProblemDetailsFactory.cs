namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Traduz um <see cref="DomainError"/> isolado para os campos <c>status</c>/<c>type</c>/
/// <c>title</c>/<c>code</c> RFC 9457 (ADR-0023/ADR-0024) — o mesmo trio que
/// <c>IDomainErrorMapper</c> resolve, sem repetir a lógica de fallback ("código não
/// mapeado" → 400/<c>uniplus.erro_nao_mapeado</c>) em cada lugar que precisa dela.
/// </summary>
public static class DomainErrorProblemDetailsFactory
{
    public static (int Status, string Type, string Title, string Code, bool RetryableConflict) Resolve(
        DomainError error, IDomainErrorMapper mapper)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(mapper);

        DomainErrorMapping resolvido = mapper.TryGetMapping(error.Code, out DomainErrorMapping? mapping)
            ? mapping
            : ErrosDaFronteiraHttp.ErroDeDominioNaoMapeado;

        // Erro não mapeado nunca é retentável: não se afirma sobre um código que o catálogo não
        // conhece, e a ausência do campo é o sinal de que o cliente não deve contar com a repetição.
        bool retryable = mapping is { RetryableConflict: true };

        return (resolvido.Status, mapper.GetProblemTypeUri(resolvido.Code), resolvido.Title, resolvido.Code, retryable);
    }
}
