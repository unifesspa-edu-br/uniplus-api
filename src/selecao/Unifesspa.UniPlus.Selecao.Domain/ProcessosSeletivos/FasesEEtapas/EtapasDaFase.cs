namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;

/// <summary>A relação entre a fase e as etapas que acontecem nela, que só quem relaciona as duas conhece.</summary>
internal static class EtapasDaFase
{
    /// <summary>As etapas que acontecem na fase: as que declaram o código dela.</summary>
    internal static IEnumerable<EtapaDeclarada> DaFase(this EtapasDoProcesso etapas, FaseDeclarada fase)
    {
        ArgumentNullException.ThrowIfNull(etapas);
        ArgumentNullException.ThrowIfNull(fase);

        return etapas.Etapas.Where(etapa => string.Equals(etapa.FaseCodigo, fase.Codigo, StringComparison.Ordinal));
    }
}
