namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A precondição de concorrência que uma mutação carrega — o <c>If-Match</c> já
/// <b>decodificado</b> (ADR-0110 D5).
/// </summary>
/// <remarks>
/// <para>
/// É um value object <b>semântico</b>, não sintático: o que chega aqui é o resultado do
/// parse do header, nunca o header cru. A gramática do <c>If-Match</c> (aspas, lista,
/// curinga, <c>W/</c>) é assunto do boundary HTTP, que a valida e recusa com <b>400</b>
/// o que for malformado (ADR-0031 — o wire se decodifica na borda). O domínio recebe o
/// que sobrou e só decide uma coisa: <b>casa ou não casa</b>.
/// </para>
/// <para>
/// <b>Weak tags não chegam.</b> A comparação exigida pelo <c>If-Match</c> é a
/// <b>forte</b> (RFC 9110 §13.1.1), e uma <c>W/"..."</c> nunca casa nela. Ela não é erro
/// de sintaxe — a gramática a aceita —, então o boundary a descarta da lista em vez de
/// recusar a requisição. O efeito é o correto: um <c>If-Match</c> só com weak tags chega
/// aqui como uma lista <b>vazia</b> de tags fortes, não casa, e sai <b>412</b> — nunca
/// 400.
/// </para>
/// </remarks>
public sealed class PrecondicaoIfMatch
{
    private readonly IReadOnlyList<string> _tags;
    private readonly bool _curinga;

    private PrecondicaoIfMatch(bool presente, bool curinga, IReadOnlyList<string> tags)
    {
        Presente = presente;
        _curinga = curinga;
        _tags = tags;
    }

    /// <summary>Nenhum <c>If-Match</c> veio na requisição.</summary>
    public static PrecondicaoIfMatch Ausente { get; } = new(presente: false, curinga: false, []);

    /// <summary><c>If-Match: *</c> — "qualquer representação existente" (RFC 9110 §13.1.1).</summary>
    public static PrecondicaoIfMatch Curinga { get; } = new(presente: true, curinga: true, []);

    /// <summary>
    /// Uma ou mais entity-tags <b>fortes</b>. A lista pode ser <b>vazia</b> — é o que
    /// resta de um <c>If-Match</c> composto só de weak tags, e ela deliberadamente não
    /// casa com nada.
    /// </summary>
    public static PrecondicaoIfMatch DeTags(IReadOnlyList<string> tagsFortes)
    {
        ArgumentNullException.ThrowIfNull(tagsFortes);
        return new PrecondicaoIfMatch(presente: true, curinga: false, [.. tagsFortes]);
    }

    /// <summary>O cliente forneceu a precondição (mesmo que ela não venha a casar).</summary>
    public bool Presente { get; }

    /// <summary>
    /// Comparação <b>forte</b> (RFC 9110 §13.1.1): o curinga casa com qualquer
    /// representação existente; uma lista casa se <b>alguma</b> das suas tags for
    /// idêntica à atual.
    /// </summary>
    public bool Casa(string etagAtual)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(etagAtual);

        if (!Presente)
        {
            return false;
        }

        return _curinga || _tags.Any(tag => string.Equals(tag, etagAtual, StringComparison.Ordinal));
    }
}
