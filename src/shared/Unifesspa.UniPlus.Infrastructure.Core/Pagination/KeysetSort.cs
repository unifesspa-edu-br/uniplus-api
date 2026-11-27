namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

using System.Linq.Expressions;

using MR.EntityFrameworkCore.KeysetPagination;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Uma coluna da ordenação keyset: o que ordena, em que sentido, e como o valor
/// daquela coluna vira texto para viajar no ponto de continuação do cursor.
/// </summary>
/// <remarks>
/// Nada compara o texto: o ponto de continuação é remontado com os valores tipados e a
/// consulta compara as colunas no banco. Por isso o texto precisa voltar ao mesmo valor, sem
/// perda: data e número em ISO-8601 ou largura fixa, nunca com a cultura corrente.
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
    /// <param name="selector">A coluna, como a biblioteca de seek a enxerga na consulta.</param>
    /// <param name="continuationKey">
    /// Valor da coluna, em texto, para o item que vira ponto de continuação da página.
    /// </param>
    /// <param name="direction">Sentido da coluna.</param>
    public static KeysetSortColumn<T> For<TColumn>(
        string token,
        Expression<Func<T, TColumn>> selector,
        Func<T, string> continuationKey,
        SortDirection direction = SortDirection.Ascending)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(continuationKey);

        return new KeysetSortColumn<T>(
            token,
            direction,
            (builder, sentido) => AplicarSentido(builder, selector, sentido),
            continuationKey);
    }

    private static void AplicarSentido<TColumn>(
        KeysetPaginationBuilder<T> builder, Expression<Func<T, TColumn>> selector, SortDirection direction)
    {
        if (direction == SortDirection.Descending)
        {
            builder.Descending(selector);
        }
        else
        {
            builder.Ascending(selector);
        }
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
/// prioridade, as condições do filtro sobre as quais ela corre e como remontar o
/// ponto de continuação a partir da chave que viajou no cursor.
/// </summary>
/// <remarks>
/// <para>O <c>Id</c> é acrescentado pela paginação como último critério e não faz
/// parte desta lista: sem ele a ordenação não seria total, e duas linhas
/// empatadas em todas as colunas poderiam se repetir ou sumir entre páginas.</para>
/// <para>Escolher as colunas em tempo de execução — a partir de parâmetros de
/// consulta, por exemplo — é montar uma instância diferente desta especificação;
/// a paginação não muda.</para>
/// <para><b>Até onde o cursor é estável:</b> a continuação retoma da posição que o
/// ponto de continuação ocupava <i>quando foi emitido</i>. Editar uma coluna de ordenação move a
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
    /// <param name="buildContinuationPoint">
    /// Monta o objeto do ponto de continuação a partir dos valores das colunas (na mesma ordem
    /// de <paramref name="columns"/>) e do <c>Id</c>. O objeto precisa expor uma
    /// propriedade por coluna do keyset, com o mesmo nome que a consulta usa. Valor que não
    /// consegue ler recusa a continuação com <see cref="CursorAnchorMismatchException"/>, nunca cai num padrão.
    /// </param>
    /// <param name="conditions">
    /// O <b>recorte</b> sobre o qual esta ordenação corre: os filtros e a busca que reduziram
    /// a coleção antes do keyset.
    /// </param>
    public KeysetSort(
        IReadOnlyList<KeysetSortColumn<T>> columns,
        Func<IReadOnlyList<string>, Guid, object> buildContinuationPoint,
        CondicoesDoFiltro conditions)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(buildContinuationPoint);
        ArgumentNullException.ThrowIfNull(conditions);
        if (columns.Count == 0)
            throw new ArgumentException("A ordenação precisa de ao menos uma coluna.", nameof(columns));

        Columns = columns;
        Conditions = conditions;
        BuildContinuationPoint = buildContinuationPoint;
        Signature = conditions.Resumo(columns.SelectMany(static c => new[] { c.Token, c.Direction == SortDirection.Descending ? "desc" : "asc" }));
    }

    /// <summary>Colunas na ordem de prioridade.</summary>
    public IReadOnlyList<KeysetSortColumn<T>> Columns { get; }

    internal CondicoesDoFiltro Conditions { get; }

    /// <summary>
    /// Resumo da consulta: as colunas e seus sentidos, <b>as condições do filtro</b> e o instante
    /// congelado. Viaja na chave do ponto de continuação para que um cursor só continue a consulta que o emitiu.
    /// </summary>
    /// <remarks>
    /// O recorte entra junto porque o ponto de continuação é uma posição <i>dentro de um
    /// conjunto</i>: continuar com outro filtro é retomar de uma posição que não
    /// existe naquele conjunto, e o seek passaria adiante de linhas que deveria
    /// devolver. Resumir só as colunas deixaria essa porta aberta.
    /// </remarks>
    internal string Signature { get; }

    internal Func<IReadOnlyList<string>, Guid, object> BuildContinuationPoint { get; }

    internal void ConfigureKeyset(KeysetPaginationBuilder<T> builder)
    {
        foreach (KeysetSortColumn<T> column in Columns)
        {
            column.Apply(builder);
        }

        // Desempate final: sem ordem total, a página não é reprodutível.
        builder.Ascending(e => e.Id);
    }

    internal string ContinuationKey(T item) =>
        ChaveDeContinuacao.Compor(Signature, Conditions.InstanteCongelado, Columns.Select(c => c.ExtractKey(item)));

    /// <summary>
    /// Reconstrói o ponto de continuação a partir da chave que veio no cursor. Devolve
    /// <see langword="false"/> quando a chave não corresponde a esta consulta —
    /// o chamador recusa a continuação em vez de paginar de um ponto arbitrário.
    /// </summary>
    /// <remarks>
    /// A conferência é pelo resumo, não pelo número de colunas: duas ordenações
    /// de mesma largura — ordenar por nome ou por código, um sentido ou o outro —
    /// produzem chaves indistinguíveis pela contagem. Aceitar a chave de uma como
    /// ponto de continuação da outra faria o seek partir de valores que não são os daquelas
    /// colunas, pulando ou repetindo registros em silêncio.
    /// </remarks>
    internal bool TryBuildContinuationPoint(string? key, Guid id, out object continuationPoint)
    {
        continuationPoint = null!;

        if (!ChaveDeContinuacao.TentarLer(key, Columns.Count, out ChaveDeContinuacao.LeituraDaChave leitura)
            || !string.Equals(leitura.Resumo, Signature, StringComparison.Ordinal))
        {
            return false;
        }

        continuationPoint = BuildContinuationPoint(leitura.Colunas, id);
        return true;
    }
}
