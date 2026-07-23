namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Xunit;

/// <summary>
/// A ordem da chave de conteúdo é a dos <b>bytes</b>, e ela não é a ordem ordinal do texto
/// decodificado.
/// </summary>
/// <remarks>
/// Sob o perfil <c>canonical-json/sha256@v1</c> as duas coincidem, porque tudo o que ele emite
/// é ASCII. A divergência aparece com texto UTF-8 literal — e é para o dia em que um perfil o
/// emitir que estes testes existem: um deles compara exatamente o par que inverte.
/// </remarks>
public sealed class ComparadorLexicograficoDeBytesTests
{
    private static byte[] Utf8(string texto) => Encoding.UTF8.GetBytes(texto);

    [Fact(DisplayName = "Comparador — ordena por octeto, resolvendo o prefixo pelo comprimento")]
    public void Ordena_PorOcteto()
    {
        ComparadorLexicograficoDeBytes comparador = ComparadorLexicograficoDeBytes.Instancia;

        comparador.Compare([1, 2], [1, 3]).Should().BeNegative();
        comparador.Compare([1, 2], [1, 2]).Should().Be(0);
        comparador.Compare([1, 2], [1]).Should().BePositive("o prefixo vem antes de quem o estende");
        comparador.Compare([0x7F], [0x80]).Should().BeNegative("os octetos são comparados sem sinal");
    }

    /// <summary>
    /// O caso que inverte. Em UTF-8, o caractere de uso privado <c>U+E000</c> começa com
    /// <c>0xEE</c> e o emoji com <c>0xF0</c> — o primeiro vem antes. Em UTF-16, o emoji é um par
    /// substituto que começa em <c>U+D83D</c>, abaixo de <c>U+E000</c> — e a comparação ordinal
    /// diz o contrário. Decodificar os bytes para comparar não é neutro.
    /// </summary>
    [Fact(DisplayName = "Comparador — diverge da comparação ordinal de texto acima do BMP")]
    public void Diverge_DaComparacaoOrdinalDeTexto()
    {
        const string UsoPrivado = "\uE000";
        const string Emoji = "\U0001F600";

        Utf8(UsoPrivado)[0].Should().Be(0xEE);
        Utf8(Emoji)[0].Should().Be(0xF0);

        ComparadorLexicograficoDeBytes.Instancia.Compare(Utf8(UsoPrivado), Utf8(Emoji))
            .Should().BeNegative("em UTF-8, 0xEE precede 0xF0");

        StringComparer.Ordinal.Compare(UsoPrivado, Emoji)
            .Should().BePositive("em UTF-16, o par substituto do emoji começa abaixo de U+E000 — a ordem se inverte");
    }

    [Fact(DisplayName = "Comparador — trata ausência sem estourar, como manda o contrato de IComparer")]
    public void Trata_Ausencia()
    {
        ComparadorLexicograficoDeBytes comparador = ComparadorLexicograficoDeBytes.Instancia;

        comparador.Compare(null, null).Should().Be(0);
        comparador.Compare(null, []).Should().BeNegative();
        comparador.Compare([], null).Should().BePositive();
    }
}
