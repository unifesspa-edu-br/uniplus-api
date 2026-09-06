namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

using System.Linq.Expressions;

using MR.EntityFrameworkCore.KeysetPagination;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>Sentido de uma coluna dentro da ordenação.</summary>
public enum DirecaoOrdenacao
{
    /// <summary>Crescente.</summary>
    Ascendente = 0,

    /// <summary>Decrescente.</summary>
    Descendente = 1,
}

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
public sealed class ColunaOrdenacaoKeyset<T>
    where T : class
{
    private readonly Action<KeysetPaginationBuilder<T>, DirecaoOrdenacao> _aplicar;
    private readonly Func<T, string> _extrair;

    private ColunaOrdenacaoKeyset(
        string token,
        DirecaoOrdenacao direcao,
        Action<KeysetPaginationBuilder<T>, DirecaoOrdenacao> aplicar,
        Func<T, string> extrair)
    {
        Token = token;
        Direcao = direcao;
        _aplicar = aplicar;
        _extrair = extrair;
    }

    /// <summary>
    /// Nome público da coluna — o mesmo que um cliente informaria para escolher a
    /// ordenação. Identifica a coluna em mensagens de erro e no catálogo de
    /// ordenações aceitas por um recurso.
    /// </summary>
    public string Token { get; }

    /// <summary>Sentido desta coluna.</summary>
    public DirecaoOrdenacao Direcao { get; }

    /// <summary>
    /// Declara uma coluna da ordenação.
    /// </summary>
    /// <param name="token">Nome público da coluna.</param>
    /// <param name="seletor">A coluna, como o motor de seek a enxerga na consulta.</param>
    /// <param name="chaveDaAncora">
    /// Valor da coluna, em texto, para o item que vira âncora da página.
    /// </param>
    /// <param name="direcao">Sentido da coluna.</param>
    public static ColunaOrdenacaoKeyset<T> De<TColuna>(
        string token,
        Expression<Func<T, TColuna>> seletor,
        Func<T, string> chaveDaAncora,
        DirecaoOrdenacao direcao = DirecaoOrdenacao.Ascendente)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        ArgumentNullException.ThrowIfNull(seletor);
        ArgumentNullException.ThrowIfNull(chaveDaAncora);

        return new ColunaOrdenacaoKeyset<T>(
            token,
            direcao,
            (builder, sentido) =>
            {
                if (sentido == DirecaoOrdenacao.Descendente)
                {
                    builder.Descending(seletor);
                }
                else
                {
                    builder.Ascending(seletor);
                }
            },
            chaveDaAncora);
    }

    /// <summary>
    /// Devolve a mesma coluna no sentido informado. O sentido é resolvido na hora
    /// de montar a consulta, e não na declaração da coluna — inverter uma coluna
    /// declarada ascendente produz de fato uma consulta descendente.
    /// </summary>
    public ColunaOrdenacaoKeyset<T> Com(DirecaoOrdenacao direcao) =>
        direcao == Direcao
            ? this
            : new ColunaOrdenacaoKeyset<T>(Token, direcao, _aplicar, _extrair);

    internal void Aplicar(KeysetPaginationBuilder<T> builder) => _aplicar(builder, Direcao);

    internal string ExtrairChave(T item) => _extrair(item);
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
public sealed class OrdenacaoKeyset<T>
    where T : class, IIdentificavel
{
    /// <param name="colunas">Colunas na ordem de prioridade; ao menos uma.</param>
    /// <param name="montarAncora">
    /// Monta o objeto de âncora a partir dos valores das colunas (na mesma ordem
    /// de <paramref name="colunas"/>) e do <c>Id</c>. O objeto precisa expor uma
    /// propriedade por coluna do keyset, com o mesmo nome que a consulta usa.
    /// </param>
    public OrdenacaoKeyset(
        IReadOnlyList<ColunaOrdenacaoKeyset<T>> colunas,
        Func<IReadOnlyList<string>, Guid, object> montarAncora)
    {
        ArgumentNullException.ThrowIfNull(colunas);
        ArgumentNullException.ThrowIfNull(montarAncora);
        if (colunas.Count == 0)
        {
            throw new ArgumentException("A ordenação precisa de ao menos uma coluna.", nameof(colunas));
        }

        Colunas = colunas;
        MontarAncora = montarAncora;
        Assinatura = string.Join(
            '|',
            colunas.Select(static c =>
                $"{c.Token}:{(c.Direcao == DirecaoOrdenacao.Descendente ? "desc" : "asc")}"));
    }

    /// <summary>Colunas na ordem de prioridade.</summary>
    public IReadOnlyList<ColunaOrdenacaoKeyset<T>> Colunas { get; }

    /// <summary>
    /// Identifica esta ordenação pelas colunas e seus sentidos, na ordem de
    /// prioridade. Viaja na chave da âncora para que um cursor só continue a
    /// ordenação que o emitiu.
    /// </summary>
    internal string Assinatura { get; }

    internal Func<IReadOnlyList<string>, Guid, object> MontarAncora { get; }

    internal void ConfigurarKeyset(KeysetPaginationBuilder<T> builder)
    {
        foreach (ColunaOrdenacaoKeyset<T> coluna in Colunas)
        {
            coluna.Aplicar(builder);
        }

        // Desempate final: sem ordem total, a página não é reprodutível.
        builder.Ascending(e => e.Id);
    }

    internal string ChaveDaAncora(T item) =>
        SortKeyComposta.Serializar([Assinatura, .. Colunas.Select(c => c.ExtrairChave(item))]);

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
    internal bool TentarReconstruirAncora(string chave, Guid id, out object ancora)
    {
        ancora = null!;

        if (!SortKeyComposta.TentarDesserializar(chave, Colunas.Count + 1, out IReadOnlyList<string> partes))
        {
            return false;
        }

        if (!string.Equals(partes[0], Assinatura, StringComparison.Ordinal))
        {
            return false;
        }

        ancora = MontarAncora([.. partes.Skip(1)], id);
        return true;
    }
}
