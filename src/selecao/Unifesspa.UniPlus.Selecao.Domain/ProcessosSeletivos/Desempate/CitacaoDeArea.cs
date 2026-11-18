namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>A citação de uma área do ENEM por um critério de desempate, com a posição do critério na lista e a da área na ordem dele.</summary>
internal readonly record struct CitacaoDeArea(int IndiceCriterio, int Ordem, int IndiceArea, string Codigo)
{
    /// <summary>
    /// As citações na ordem do desempate e, dentro de cada critério, na ordem das áreas: a ordem do desempate decide qual
    /// citação é a primeira, e não a posição na lista, que o comando não garante.
    /// </summary>
    internal static List<CitacaoDeArea> NaOrdemDoDesempate(IReadOnlyList<CriterioDesempateInformado> criterios) =>
    [
        .. Enumerable.Range(0, criterios.Count)
            .OrderBy(indice => criterios[indice].Ordem)
            .SelectMany(indice => criterios[indice].AreasCitadas
                .Select(area => new CitacaoDeArea(indice, criterios[indice].Ordem, area.IndiceArea, area.Codigo))),
    ];
}
