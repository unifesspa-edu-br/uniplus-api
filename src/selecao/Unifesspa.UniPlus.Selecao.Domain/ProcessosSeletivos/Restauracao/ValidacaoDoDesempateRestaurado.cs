namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Restauracao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

internal static class ValidacaoDoDesempateRestaurado
{
    // O mesmo teto da gravação: sem ele, a restauração aplicaria uma lista que o PUT seguinte da
    // mesma lista recusaria.
    internal static DomainError? CriteriosEmExcesso(GrafoConfiguracao grafo) =>
        RegrasDoDesempate.ConferirQuantidadeDeCriteriosDesempate(grafo.CriteriosDesempate.Count) is [var excesso, ..]
            ? excesso.Error
            : null;

    // A ordem duplicada é conferida à parte, e não pela conferência da edição: aquela acumula os
    // erros por índice, intercalados com os da etapa citada, e a restauração devolve só o primeiro.
    internal static DomainError? RecusaDosCriterios(GrafoConfiguracao grafo) =>
        OrdemDuplicada(grafo)
        ?? EtapaCitadaInexistente(grafo)
        ?? DesempatePorArea(grafo);

    private static DomainError? OrdemDuplicada(GrafoConfiguracao grafo)
    {
        List<int> ordens = [.. grafo.CriteriosDesempate.Select(static c => c.Ordem)];
        return ordens.Distinct().Count() != ordens.Count ? RegrasDoDesempate.OrdemDesempateDuplicada() : null;
    }

    // Contra as etapas do próprio grafo. Se o codec regenerasse o Id da etapa em vez de
    // preservá-lo, é aqui que a restauração pararia: a referência congelada deixa de resolver.
    private static DomainError? EtapaCitadaInexistente(GrafoConfiguracao grafo)
    {
        foreach (CriterioDesempate criterio in grafo.CriteriosDesempate)
        {
            if (criterio.Args is ArgsDesempateMaiorNotaEtapa args && !grafo.Etapas.Any(e => e.Id == args.EtapaRef))
            {
                return RegrasDoDesempate.EtapaRefDesempateInexistente(criterio.Ordem, args.EtapaRef);
            }
        }

        return null;
    }

    // Contra a classificação do próprio grafo: sem esta conferência, a restauração reintroduziria um
    // desempate por área que o quadro restaurado não tem.
    private static DomainError? DesempatePorArea(GrafoConfiguracao grafo) =>
        RegrasDoDesempate.PrimeiraViolacaoDoDesempatePorArea(
            RegrasDoDesempate.EmOrdem(grafo.CriteriosDesempate), grafo.Classificacao);
}
