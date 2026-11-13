namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;

using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Quem, fora da própria etapa, a cita: o desempate e as regras de eliminação da classificação.
/// </summary>
internal static class CitacoesDasEtapas
{
    /// <summary>
    /// A etapa é referenciada por configuração que não pertence à fase dela — critério de
    /// desempate por nota de etapa ou regra de eliminação que a cita.
    /// </summary>
    internal static bool EtapaCitadaPorDesempateOuClassificacao(EstadoDaConfiguracao estado, Guid etapaId) =>
        EtapaCitadaPorDesempate(estado, etapaId)
        || (estado.Classificacao?.RegrasEliminacao ?? []).Any(regra => regra.Args.EtapaReferenciada() == etapaId);

    internal static bool EtapaCitadaPorDesempate(EstadoDaConfiguracao estado, Guid etapaId) =>
        estado.CriteriosDesempate.Any(c => c.Args is ArgsDesempateMaiorNotaEtapa args && args.EtapaRef == etapaId);

    /// <summary>As regras de eliminação por nota mínima que citam a etapa.</summary>
    internal static IEnumerable<ArgsElimNotaMinimaEtapa> EliminacoesPorNotaMinimaQueCitam(EstadoDaConfiguracao estado, Guid etapaId) =>
        (estado.Classificacao?.RegrasEliminacao ?? [])
            .Select(static regra => regra.Args)
            .OfType<ArgsElimNotaMinimaEtapa>()
            .Where(args => args.EtapaRef == etapaId);
}
