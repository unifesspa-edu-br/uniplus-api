namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

using System.Linq.Expressions;

using MR.EntityFrameworkCore.KeysetPagination;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Uma coluna da ordenação keyset: o que ordena, em que sentido, e como o valor
/// daquela coluna vira texto para viajar na âncora do cursor.
/// </summary>
/// <remarks>
/// O texto da âncora tem de preservar a <b>mesma</b> ordem que a coluna tem no
/// banco — comparar as partes serializadas e comparar as colunas precisa dar o
/// mesmo resultado. Para texto isso é imediato; para data e número, formatar em
/// largura fixa ou ISO-8601, nunca com a cultura corrente.
/// </remarks>
public sealed class KeysetSortColumn<T>
    where T : class
{
    private readonly Action<KeysetPaginationBuilder<T>, SortDirection> _apply;
    private readonly Func<T, string> _extract;

    private KeysetSortColumn(
        string token,
        SortDirection direction,
        Action<KeysetPaginationBuilder<T>, SortDirection> apply,
        Func<T, string> extract)
    {
        Token = token;
        Direction = direction;
        _apply = apply;
        _extract = extract;
    }

    /// <summary>
    /// Nome público da coluna — o mesmo que um cliente informaria para escolher a
    /// ordenação. Identifica a coluna em mensagens de erro e no catálogo de
    /// ordenações aceitas por um recurso.
    /// </summary>
    public string Token { get; }

    /// <summary>Sentido desta coluna.</summary>
    public SortDirection Direction { get; }

    /// <summary>
    /// Declara uma coluna da ordenação.
    /// </summary>
    /// <param name="token">Nome público da coluna.</param>
    /// <param name="selector">A coluna, como o motor de seek a enxerga na consulta.</param>
    /// <param name="anchorKey">
    /// Valor da coluna, em texto, para o item que vira âncora da página.
    /// </param>
    /// <param name="direction">Sentido da coluna.</param>
    public static KeysetSortColumn<T> For<TColumn>(
        string token,
        Expression<Func<T, TColumn>> selector,
        Func<T, string> anchorKey,
        SortDirection direction = SortDirection.Ascending)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(anchorKey);

        return new KeysetSortColumn<T>(
            token,
            direction,
            (builder, direction) =>
            {
                if (direction == SortDirection.Descending)
                {
                    builder.Descending(selector);
                }
                else
                {
                    builder.Ascending(selector);
                }
            },
            anchorKey);
    }

    /// <summary>
    /// Devolve a mesma coluna no sentido informado. O sentido é resolvido na hora
    /// de montar a consulta, e não na declaração da coluna — inverter uma coluna
    /// declarada ascendente produz de fato uma consulta descendente.
    /// </summary>
    public KeysetSortColumn<T> With(SortDirection direction) =>
        direction == Direction
            ? this
            : new KeysetSortColumn<T>(Token, direction, _apply, _extract);

    internal void Apply(KeysetPaginationBuilder<T> builder) => _apply(builder, Direction);

    internal string ExtractKey(T item) => _extract(item);
}

/// <summary>
/// Ordenação completa de uma listagem paginada: as colunas na ordem de
/// prioridade, e como remontar a âncora de continuação a partir da chave que
/// viajou no cursor.
/// </summary>
/// <remarks>
/// <para>O <c>Id</c> é acrescentado pelo motor como último critério e não faz
/// parte desta lista: sem ele a ordenação não seria total, e duas linhas
/// empatadas em todas as colunas poderiam se repetir ou sumir entre páginas.</para>
/// <para>Escolher as colunas em tempo de execução — a partir de parâmetros de
/// consulta, por exemplo — é montar uma instância diferente desta especificação;
/// o motor de paginação não muda.</para>
/// <para><b>Até onde o cursor é estável:</b> a continuação retoma da posição que a
/// âncora ocupava <i>quando foi emitida</i>. Editar uma coluna de ordenação move a
/// linha, então uma linha já lida pode reaparecer numa página seguinte, e uma ainda
/// não lida pode passar despercebida — o mesmo que acontece quando uma linha é
/// criada ou removida no meio da travessia. É a propriedade que se troca por
/// ordenar pelo atributo que a pessoa lê, em vez de por um identificador imutável;
/// listagem administrativa convive com isso, relatório que exige recorte estável
/// pede snapshot, não cursor.</para>
/// </remarks>
public sealed class KeysetSort<T>
    where T : class, IIdentificavel
{
    /// <param name="columns">Colunas na ordem de prioridade; ao menos uma.</param>
    /// <param name="buildAnchor">
    /// Monta o objeto de âncora a partir dos valores das colunas (na mesma ordem
    /// de <paramref name="columns"/>) e do <c>Id</c>. O objeto precisa expor uma
    /// propriedade por coluna do keyset, com o mesmo nome que a consulta usa.
    /// </param>
    /// <param name="scope">
    /// Identifica o <b>recorte</b> sobre o qual esta ordenação corre: os filtros e
    /// a busca que reduziram a coleção antes do keyset. Partes na ordem em que o
    /// chamador as declara; vazio quando a listagem não tem recorte.
    /// </param>
    public KeysetSort(
        IReadOnlyList<KeysetSortColumn<T>> columns,
        Func<IReadOnlyList<string>, Guid, object> buildAnchor,
        IReadOnlyList<string>? scope = null)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(buildAnchor);
        if (columns.Count == 0)
        {
            throw new ArgumentException("A ordenação precisa de ao menos uma coluna.", nameof(columns));
        }

        Columns = columns;
        BuildAnchor = buildAnchor;

        // Pelo mesmo formato dos valores, e não por concatenação com separador: um
        // token que contivesse o separador escolhido faria duas ordenações distintas
        // produzirem a mesma assinatura, e o cursor de uma seria aceito pela outra —
        // exatamente a confusão que a assinatura existe para impedir.
        Signature = CompositeSortKey.Serialize(
            [
                .. columns.SelectMany(static c =>
                    new[] { c.Token, c.Direction == SortDirection.Descending ? "desc" : "asc" }),
                .. scope ?? [],
            ]);
    }

    /// <summary>Colunas na ordem de prioridade.</summary>
    public IReadOnlyList<KeysetSortColumn<T>> Columns { get; }

    /// <summary>
    /// Identifica esta consulta pelas colunas e seus sentidos, na ordem de
    /// prioridade, <b>e pelo recorte</b> sobre o qual ela corre. Viaja na chave da
    /// âncora para que um cursor só continue a consulta que o emitiu.
    /// </summary>
    /// <remarks>
    /// O recorte entra junto porque a âncora é uma posição <i>dentro de um
    /// conjunto</i>: continuar com outro filtro é retomar de uma posição que não
    /// existe naquele conjunto, e o seek passaria adiante de linhas que deveria
    /// devolver. Assinar só as colunas deixaria essa porta aberta.
    /// </remarks>
    internal string Signature { get; }

    internal Func<IReadOnlyList<string>, Guid, object> BuildAnchor { get; }

    internal void ConfigureKeyset(KeysetPaginationBuilder<T> builder)
    {
        foreach (KeysetSortColumn<T> column in Columns)
        {
            column.Apply(builder);
        }

        // Desempate final: sem ordem total, a página não é reprodutível.
        builder.Ascending(e => e.Id);
    }

    internal string AnchorKey(T item) =>
        CompositeSortKey.Serialize([Signature, .. Columns.Select(c => c.ExtractKey(item))]);

    /// <summary>
    /// Reconstrói a âncora a partir da chave que veio no cursor. Devolve
    /// <see langword="false"/> quando a chave não corresponde a esta ordenação —
    /// o chamador recusa a continuação em vez de paginar de um ponto arbitrário.
    /// </summary>
    /// <remarks>
    /// A conferência é pela assinatura, não pelo número de colunas: duas ordenações
    /// de mesma largura — ordenar por nome ou por código, um sentido ou o outro —
    /// produzem chaves indistinguíveis pela contagem. Aceitar a chave de uma como
    /// âncora da outra faria o seek partir de valores que não são os daquelas
    /// colunas, pulando ou repetindo registros em silêncio, que é justamente o que
    /// esta recusa existe para impedir.
    /// </remarks>
    internal bool TryBuildAnchor(string key, Guid id, out object anchor)
    {
        anchor = null!;

        if (!CompositeSortKey.TryDeserialize(key, Columns.Count + 1, out IReadOnlyList<string> parts))
        {
            return false;
        }

        if (!string.Equals(parts[0], Signature, StringComparison.Ordinal))
        {
            return false;
        }

        anchor = BuildAnchor([.. parts.Skip(1)], id);
        return true;
    }
}
