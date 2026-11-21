namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// O lugar que diz qual fase do cronograma vem antes de qual na exigência documental e no fato que decide o acréscimo.
/// Cada decisão cria a sua ordem uma vez, a partir da lista do cronograma, e a usa em todas as perguntas. A posição de
/// cada fase é o momento em que ela acontece (<see cref="MomentosPorOrdem"/>), lido da própria instância informada na
/// lista, de modo que fases com a mesma identidade não se fundem: cada uma vale pela sua.
/// </summary>
internal sealed class OrdemDoCronograma
{
    private readonly Dictionary<FaseCronograma, int> _posicoes;

    private OrdemDoCronograma(Dictionary<FaseCronograma, int> posicoes) => _posicoes = posicoes;

    /// <summary>A ordem das fases da lista do cronograma; a mesma instância repetida na lista conta uma só vez.</summary>
    public static OrdemDoCronograma Do(IEnumerable<FaseCronograma> cronograma)
    {
        ArgumentNullException.ThrowIfNull(cronograma);

        List<FaseCronograma> fases = [.. cronograma];
        IReadOnlyDictionary<int, int> momentos = MomentosPorOrdem(fases.Select(static fase => (fase.Ordem, fase.NoMesmoMomentoDaAnterior)));
        Dictionary<FaseCronograma, int> posicoes = new(ReferenceEqualityComparer.Instance);
        foreach (FaseCronograma fase in fases)
        {
            posicoes[fase] = momentos[fase.Ordem];
        }

        return new OrdemDoCronograma(posicoes);
    }

    /// <summary>
    /// O momento de cada ordem declarada: a menor ordem acontece no momento 1 e cada ordem seguinte, no momento seguinte,
    /// ou no mesmo da ordem anterior quando alguma fase dela está marcada para acontecer no mesmo momento da anterior. A
    /// ordem que vem depois de lacuna não cria momento vazio, e a marca na menor ordem não tem efeito, porque não há
    /// fase antes dela. Não depende da ordem em que as fases chegam.
    /// </summary>
    internal static IReadOnlyDictionary<int, int> MomentosPorOrdem(IEnumerable<(int Ordem, bool NoMesmoMomentoDaAnterior)> fases)
    {
        ArgumentNullException.ThrowIfNull(fases);

        Dictionary<int, int> momentos = [];
        int momento = 0;
        foreach (IGrouping<int, bool> ordem in fases.GroupBy(static fase => fase.Ordem, static fase => fase.NoMesmoMomentoDaAnterior).OrderBy(static grupo => grupo.Key))
        {
            bool acontecemJuntas = momentos.Count > 0 && ordem.Any(static marcada => marcada);
            momento = acontecemJuntas ? momento : momento + 1;
            momentos[ordem.Key] = momento;
        }

        return momentos;
    }

    /// <summary>Se a fase acontece depois da referência; fases no mesmo momento não são posteriores uma à outra.</summary>
    /// <exception cref="ArgumentException">Quando uma das fases não faz parte do cronograma informado.</exception>
    public bool Posterior(FaseCronograma fase, FaseCronograma referencia) =>
        PosicaoDe(fase, nameof(fase)) > PosicaoDe(referencia, nameof(referencia));

    /// <summary>
    /// A fase de maior posição entre as informadas, ignorando as nulas; no empate, a primeira na ordem recebida.
    /// Nula quando não há nenhuma fase.
    /// </summary>
    /// <exception cref="ArgumentException">Quando uma das fases não faz parte do cronograma informado.</exception>
    public FaseCronograma? MaisTardia(IEnumerable<FaseCronograma?> fases)
    {
        ArgumentNullException.ThrowIfNull(fases);

        List<(FaseCronograma Fase, int Posicao)> posicoes =
            [.. fases.OfType<FaseCronograma>().Select(fase => (fase, PosicaoDe(fase, nameof(fases))))];

        return posicoes.Count == 0 ? null : posicoes.MaxBy(static p => p.Posicao).Fase;
    }

    private int PosicaoDe(FaseCronograma fase, string parametro)
    {
        ArgumentNullException.ThrowIfNull(fase);

        return _posicoes.TryGetValue(fase, out int posicao)
            ? posicao
            : throw new ArgumentException("A fase não faz parte do cronograma informado.", parametro);
    }
}
