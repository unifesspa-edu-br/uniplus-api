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
    public static CriterioDesempateInformado De(Entities.CriterioDesempate criterio)
    {
        ArgumentNullException.ThrowIfNull(criterio);
        return new(criterio.Ordem, criterio.Args);
    }
}
