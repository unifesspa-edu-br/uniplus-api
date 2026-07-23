namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

/// <summary>
/// Ordem lexicográfica sobre os <b>bytes</b> — a que vale quando a chave de ordenação é o
/// próprio conteúdo canônico de um item.
/// </summary>
/// <remarks>
/// <para>
/// Onde uma coleção do envelope não tem chave natural, a ordem vem da <b>chave de conteúdo</b>:
/// os bytes canônicos do próprio item (ADR-0109 D9). Comparar essa chave decodificando os bytes
/// para <c>string</c> e usando comparação ordinal parece equivalente, e é — enquanto os bytes
/// forem ASCII, como são sob o perfil <c>canonical-json/sha256@v1</c>, que escapa todo o resto.
/// </para>
/// <para>
/// Deixa de ser no instante em que um perfil emitir texto UTF-8 literal: a ordem por unidade de
/// código UTF-16 diverge da ordem por byte UTF-8 acima de <c>U+FFFF</c>. Um caractere da área de
/// uso privado (<c>U+E000</c>) precede um emoji em UTF-8 e o sucede em comparação ordinal, porque
/// o emoji é escrito como par substituto, que começa em <c>U+D800</c>. Duas configurações
/// equivalentes ordenariam diferente conforme o texto que carregam.
/// </para>
/// <para>
/// A ordem daqui é a única compatível com "os bytes canônicos do próprio item", e é a mesma que
/// o perfil v1 já produz na prática — trocá-la agora não move nenhum envelope publicado.
/// </para>
/// </remarks>
public sealed class ComparadorLexicograficoDeBytes : IComparer<byte[]>
{
    public static readonly ComparadorLexicograficoDeBytes Instancia = new();

    private ComparadorLexicograficoDeBytes()
    {
    }

    public int Compare(byte[]? x, byte[]? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        // SequenceCompareTo sobre byte compara sem sinal e resolve o prefixo pelo comprimento —
        // que é exatamente a ordem lexicográfica de octetos.
        return x.AsSpan().SequenceCompareTo(y.AsSpan());
    }
}
