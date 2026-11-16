namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>A causa que um portão devolve quando as regras acumulam várias: a primeira, na ordem em que as regras orientam.</summary>
internal static class CausasDaRecusa
{
    internal static DomainError? Primeira(IReadOnlyList<FieldError> recusas)
    {
        ArgumentNullException.ThrowIfNull(recusas);

        return recusas.Count > 0 ? recusas[0].Error : null;
    }
}
