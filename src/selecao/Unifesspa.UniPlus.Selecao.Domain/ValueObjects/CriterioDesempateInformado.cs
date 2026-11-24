namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Um critério de desempate como o comando o informou, na posição dele: a ordem, que vem
/// sempre, e os parâmetros, quando foi possível montá-los. É o que o processo confere contra si
/// mesmo (ordem única, etapa existente, área no quadro de pesos por área) — inclusive para
/// o critério que o próprio <c>CriterioDesempate</c> recusou, para que a resposta traga de
/// uma vez todas as recusas de cada item.
/// </summary>
/// <param name="Parametros"><see langword="null"/> quando a regra não foi resolvida no catálogo.</param>
public readonly record struct CriterioDesempateInformado(int Ordem, ParametrosCriterioDesempate? Parametros)
{
    /// <summary>A etapa que o critério compara, quando é o de maior nota na etapa.</summary>
    public Guid? EtapaCitada => Parametros is ParametrosDesempateMaiorNotaEtapa porEtapa ? porEtapa.EtapaId : null;

    public bool ComparaAreaDoEnem => Parametros is ParametrosDesempateMaiorNotaAreaEnem;

    /// <summary>As áreas bem formadas que o critério cita, com a posição de cada uma na ordem declarada.</summary>
    public IEnumerable<(int IndiceArea, string Codigo)> AreasCitadas =>
        Parametros is ParametrosDesempateMaiorNotaAreaEnem porArea ? porArea.ConferirAreas().BemFormadas : [];

    public static CriterioDesempateInformado De(Entities.CriterioDesempate criterio)
    {
        ArgumentNullException.ThrowIfNull(criterio);
        return new(criterio.Ordem, criterio.Parametros);
    }
}
