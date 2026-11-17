namespace Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// As fases e as etapas em que o processo pode decidir, as fases por ordem e depois as etapas por ordem. É o único
/// lugar que sabe se uma fase ou etapa existe no processo, e a diferença entre dois retratos dele diz quais saíram.
/// </summary>
public sealed class LugaresDaDecisao
{
    private readonly IReadOnlyList<LugarNoProcesso> _lugares;

    private LugaresDaDecisao(IReadOnlyList<LugarNoProcesso> lugares) => _lugares = lugares;

    /// <summary>Retrato dos lugares do processo no estado em que ele está agora.</summary>
    public static LugaresDaDecisao Do(ProcessoSeletivo processo)
    {
        ArgumentNullException.ThrowIfNull(processo);

        IEnumerable<LugarNoProcesso> fases = processo.CronogramaFases.OrderBy(static f => f.Ordem).Select(static f => new LugarNoProcesso(
            LugarDaDecisao.DaFase(f.FaseCanonicaOrigemId), $"cronograma.fases[{f.Codigo}]", $"A fase '{f.Codigo}'"));
        IEnumerable<LugarNoProcesso> etapas = processo.Etapas.OrderBy(static e => e.Ordem).Select(static e => new LugarNoProcesso(
            LugarDaDecisao.DaEtapa(e.Id), $"cronograma.etapas[{e.Nome}]", $"A etapa '{e.Nome}'"));

        return new([.. fases, .. etapas]);
    }

    public bool Contem(LugarDaDecisao lugar) => _lugares.Any(l => l.Lugar == lugar);

    /// <summary>Os lugares deste retrato que o retrato posterior não traz, na mesma ordem.</summary>
    internal IEnumerable<LugarNoProcesso> AusentesEm(LugaresDaDecisao depois)
    {
        ArgumentNullException.ThrowIfNull(depois);

        return _lugares.Where(l => !depois.Contem(l.Lugar));
    }
}

/// <summary>Um lugar da decisão com o campo que o localiza na recusa e o nome dele na mensagem.</summary>
internal sealed record LugarNoProcesso(LugarDaDecisao Lugar, string Campo, string Descricao);
