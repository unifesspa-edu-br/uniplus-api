namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>As recusas de uma regra, uma ou várias, como a lista de causas que as famílias acumulam.</summary>
internal static class CausasDaRecusa
{
    internal static IReadOnlyList<FieldError> Da(DomainError? recusa, string? campo = null) =>
        recusa is null ? [] : [new FieldError(campo, recusa)];

    internal static IReadOnlyList<FieldError> Da(IEnumerable<DomainError> recusas, string? campo = null)
    {
        ArgumentNullException.ThrowIfNull(recusas);

        return [.. recusas.Select(recusa => new FieldError(campo, recusa))];
    }

    internal static IReadOnlyList<FieldError> NoCampo(IEnumerable<FieldError> causas, string campo)
    {
        ArgumentNullException.ThrowIfNull(causas);

        return [.. causas.Select(causa => causa with { Field = campo })];
    }
}
