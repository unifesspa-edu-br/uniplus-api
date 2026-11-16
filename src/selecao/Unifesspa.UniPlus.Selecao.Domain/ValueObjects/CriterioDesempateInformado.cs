namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Um critério de desempate como o comando o informou, na posição dele: a ordem, que vem
/// sempre, e os args, quando foi possível montá-los. É o que o processo confere contra si
/// mesmo (ordem única, etapa existente, área no quadro de pesos por área) — inclusive para
/// o critério que o próprio <c>CriterioDesempate</c> recusou, para que a resposta traga de
/// uma vez todas as recusas de cada item.
/// </summary>
/// <param name="Args"><see langword="null"/> quando a regra não foi resolvida no catálogo.</param>
public readonly record struct CriterioDesempateInformado(int Ordem, ArgsCriterioDesempate? Args)
{
    /// <summary>A etapa que o critério compara, quando é o de maior nota na etapa.</summary>
    public Guid? EtapaCitada => Args is ArgsDesempateMaiorNotaEtapa porEtapa ? porEtapa.EtapaRef : null;

    public bool ComparaAreaDoEnem => Args is ArgsDesempateMaiorNotaAreaEnem;

    /// <summary>As áreas bem formadas que o critério cita, com a posição de cada uma na ordem declarada.</summary>
    public IEnumerable<(int IndiceArea, string Codigo)> AreasCitadas =>
        Args is ArgsDesempateMaiorNotaAreaEnem porArea ? Entities.CriterioDesempate.ConferirAreas(porArea.Areas).BemFormadas : [];

    public static CriterioDesempateInformado De(Entities.CriterioDesempate criterio)
    {
        ArgumentNullException.ThrowIfNull(criterio);
        return new(criterio.Ordem, criterio.Args);
    }
}
