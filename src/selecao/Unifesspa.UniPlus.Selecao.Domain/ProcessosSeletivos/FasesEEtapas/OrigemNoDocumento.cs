namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

/// <summary>
/// Onde cada parte da configuração declarada está no documento recebido. A declaração entrega cada lista na ordem canônica
/// (<see cref="OrdemCanonica.OrdemCanonicaDaConfiguracao"/>), e quem lê o documento o recebe na ordem do autor: esta origem
/// guarda, por lista, a posição de origem de cada posição canônica, e as partes que a leitura recusou. O ponteiro de uma causa
/// aponta a posição de origem (<see cref="PonteiroNoDocumento"/>), e a tradução é feita num lugar só, na hora de montá-lo.
/// A lista se nomeia pelo caminho dela no campo da regra, com a posição canônica de cada item do caminho: <c>fases</c>,
/// <c>fases[1].bancasRequeridas</c>. A lista que a origem não guarda está na ordem do documento. A configuração que vem da raiz
/// é a <see cref="Identidade"/>: a posição de origem é a canônica e nada foi recusado.
/// </summary>
public sealed record OrigemNoDocumento
{
    /// <summary>A origem da configuração que vem da raiz.</summary>
    public static OrigemNoDocumento Identidade { get; } = new(new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal), []);

    private readonly IReadOnlyDictionary<string, IReadOnlyList<int>> _posicoesDeOrigem;

    internal OrigemNoDocumento(IReadOnlyDictionary<string, IReadOnlyList<int>> posicoesDeOrigem, IReadOnlyList<ParteRecusada> partesRecusadas)
    {
        ArgumentNullException.ThrowIfNull(posicoesDeOrigem);
        ArgumentNullException.ThrowIfNull(partesRecusadas);

        _posicoesDeOrigem = posicoesDeOrigem;
        PartesRecusadas = partesRecusadas;
    }

    /// <summary>As partes que a leitura recusou, na ordem em que o documento as traz.</summary>
    public IReadOnlyList<ParteRecusada> PartesRecusadas { get; }

    /// <summary>
    /// As partes recusadas do item, pelo caminho que o item lhes dá (<c>produtos</c>, <c>bancasRequeridas</c>, <c>unidadeResponsavel</c>):
    /// o item se nomeia pelo caminho dele na lista da declaração, com a posição canônica (<c>fases[1]</c>).
    /// </summary>
    public IReadOnlySet<string> PartesRecusadasDe(string item)
    {
        ArgumentException.ThrowIfNullOrEmpty(item);

        string prefixo = item + ".";

        return PartesRecusadas
            .Where(parte => parte.Caminho.StartsWith(prefixo, StringComparison.Ordinal))
            .Select(parte => parte.Caminho[prefixo.Length..])
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Se a leitura recusou alguma parte no caminho dado: a lista (<c>fases</c>) ou o campo (<c>fases[1].unidadeResponsavel</c>).</summary>
    public bool TemParteRecusadaEm(string caminho) =>
        PartesRecusadas.Any(parte => string.Equals(parte.Caminho, caminho, StringComparison.Ordinal));

    /// <summary>
    /// A origem de uma lista do documento: <paramref name="doDocumento"/> traz os itens que a leitura aceitou, cada um com a posição
    /// em que veio, e a posição canônica de cada item é a que <paramref name="ordemCanonica"/> lhe dá.
    /// </summary>
    internal static OrigemNoDocumento DaLista<T>(
        string lista,
        IReadOnlyList<(T Item, int PosicaoDeOrigem)> doDocumento,
        Func<IEnumerable<T>, IOrderedEnumerable<T>> ordemCanonica,
        IReadOnlyList<ParteRecusada> partesRecusadas)
        where T : class
    {
        ArgumentException.ThrowIfNullOrEmpty(lista);
        ArgumentNullException.ThrowIfNull(doDocumento);
        ArgumentNullException.ThrowIfNull(ordemCanonica);

        int[] naOrdemCanonica =
            [.. ordemCanonica(doDocumento.Select(static par => par.Item)).Select(item => doDocumento.First(par => ReferenceEquals(par.Item, item)).PosicaoDeOrigem)];

        return new(new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal) { [lista] = naOrdemCanonica }, partesRecusadas);
    }

    /// <summary>A origem de um bloco que não tem lista: só as partes que a leitura recusou nele.</summary>
    internal static OrigemNoDocumento SoComPartesRecusadas(IReadOnlyList<ParteRecusada> partesRecusadas) =>
        new(new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal), partesRecusadas);

    /// <summary>A origem do documento inteiro: as listas e as partes recusadas de cada bloco, na ordem dos blocos.</summary>
    internal static OrigemNoDocumento Reunir(IEnumerable<OrigemNoDocumento> dosBlocos)
    {
        ArgumentNullException.ThrowIfNull(dosBlocos);

        OrigemNoDocumento[] blocos = [.. dosBlocos];

        return new(
            blocos.SelectMany(static bloco => bloco._posicoesDeOrigem).ToDictionary(static lista => lista.Key, static lista => lista.Value, StringComparer.Ordinal),
            [.. blocos.SelectMany(static bloco => bloco.PartesRecusadas)]);
    }

    /// <summary>A posição, na lista do documento recebido, do item que a declaração entrega na posição canônica dada.</summary>
    public int PosicaoDeOrigem(string lista, int posicaoCanonica)
    {
        ArgumentException.ThrowIfNullOrEmpty(lista);

        if (!_posicoesDeOrigem.TryGetValue(lista, out IReadOnlyList<int>? posicoes))
        {
            return posicaoCanonica;
        }

        return (uint)posicaoCanonica < (uint)posicoes.Count
            ? posicoes[posicaoCanonica]
            : throw new ArgumentOutOfRangeException(
                nameof(posicaoCanonica), posicaoCanonica, $"A lista '{lista}' não tem a posição canônica {posicaoCanonica} na origem.");
    }
}
