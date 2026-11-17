namespace Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// As fases e as etapas em que o processo pode decidir. É o único lugar que sabe se uma fase ou etapa existe no
/// processo, e a diferença entre dois retratos dele diz quais saíram.
/// </summary>
public sealed class LugaresDaDecisao
{
    private readonly IReadOnlyList<LugarDaDecisao> _lugares;

    private LugaresDaDecisao(IReadOnlyList<LugarDaDecisao> lugares) => _lugares = lugares;

    /// <summary>Retrato dos lugares do processo no estado em que ele está agora.</summary>
    public static LugaresDaDecisao Do(ProcessoSeletivo processo)
    {
        ArgumentNullException.ThrowIfNull(processo);

        IEnumerable<LugarDaDecisao> fases = processo.CronogramaFases.Select(static f => LugarDaDecisao.DaFase(f.FaseCanonicaOrigemId));
        IEnumerable<LugarDaDecisao> etapas = processo.Etapas.Select(static e => LugarDaDecisao.DaEtapa(e.Id));

        return new([.. fases, .. etapas]);
    }

    public bool Contem(LugarDaDecisao lugar) => _lugares.Any(l => l == lugar);

    /// <summary>Os lugares deste retrato que o retrato posterior não traz.</summary>
    internal IEnumerable<LugarDaDecisao> AusentesEm(LugaresDaDecisao depois)
    {
        ArgumentNullException.ThrowIfNull(depois);

        return _lugares.Where(l => !depois.Contem(l));
    }
}
