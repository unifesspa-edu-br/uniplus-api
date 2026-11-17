namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Restauracao;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Os critérios de desempate do grafo restaurado, pelas mesmas regras de <c>DefinirCriteriosDesempate</c>: o teto de
/// critérios, a ordem única, a etapa citada e o desempate por área contra a classificação do grafo.
/// </summary>
internal static class ValidacaoDoDesempateRestaurado
{
    private const string Campo = "classificacao";

    internal static IReadOnlyList<FieldError> Validar(GrafoConfiguracao grafo) =>
    [
        .. CausasDaRecusa.NoCampo(RegrasDoDesempate.ConferirQuantidadeDeCriteriosDesempate(grafo.CriteriosDesempate.Count), Campo),
        .. CausasDaRecusa.Da(OrdemRepetida(grafo), Campo),
        .. EtapasCitadasInexistentes(grafo).Select(static erro => new FieldError(Campo, erro)),
        .. CausasDaRecusa.Da(
            RegrasDoDesempate.PrimeiraViolacaoDoDesempatePorArea(RegrasDoDesempate.EmOrdem(grafo.CriteriosDesempate), grafo.Classificacao), Campo),
    ];

    private static DomainError? OrdemRepetida(GrafoConfiguracao grafo) =>
        grafo.CriteriosDesempate.Select(static c => c.Ordem).Distinct().Count() != grafo.CriteriosDesempate.Count
            ? new DomainError(
                "ProcessoSeletivo.OrdemDesempateDuplicada",
                "Cada critério de desempate deve ter uma ordem única dentro do processo.")
            : null;

    // Contra as etapas do grafo: se o codec regenerasse o Id da etapa em vez de preservá-lo, a restauração pararia
    // aqui, porque a referência congelada deixaria de resolver.
    private static IEnumerable<DomainError> EtapasCitadasInexistentes(GrafoConfiguracao grafo) =>
        grafo.CriteriosDesempate
            .Where(criterio => criterio.Args is ArgsDesempateMaiorNotaEtapa args && !grafo.Etapas.Any(etapa => etapa.Id == args.EtapaRef))
            .Select(static criterio => ReferenciaAEtapaInexistente.PeloDesempate(criterio.Ordem, ((ArgsDesempateMaiorNotaEtapa)criterio.Args).EtapaRef));
}
