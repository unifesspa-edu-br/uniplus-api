namespace Unifesspa.UniPlus.Kernel.UnitTests.Domain.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

public sealed class FormaParaComparacaoTests
{
    private const string LetrasAcentuadas = "ÁÀÂÃÄÅÉÈÊËÍÌÎÏÓÒÔÕÖÚÙÛÜÇÑÝáàâãäåéèêëíìîïóòôõöúùûüçñý";
    private const string LetrasSemAcentoEmMinuscula = "aaaaaaeeeeiiiiooooouuuucnyaaaaaaeeeeiiiiooooouuuucny";

    public static TheoryData<string, string> LetrasDaTabela()
    {
        TheoryData<string, string> letras = [];

        for (int posicao = 0; posicao < LetrasAcentuadas.Length; posicao++)
        {
            letras.Add(LetrasAcentuadas[posicao].ToString(), LetrasSemAcentoEmMinuscula[posicao].ToString());
        }

        return letras;
    }

    [Fact]
    public void LetrasDaTabela_SaoCinquentaEDuas() => LetrasDaTabela().Should().HaveCount(52);

    [Theory(DisplayName = "Cada letra acentuada da tabela vira a letra sem acento, em minúscula")]
    [MemberData(nameof(LetrasDaTabela))]
    public void De_LetraAcentuada_DevolveALetraSemAcentoEmMinuscula(string letra, string esperada) =>
        FormaParaComparacao.De(letra).Should().Be(esperada);

    [Theory(DisplayName = "Texto sem acento e em minúsculas passa idêntico")]
    [InlineData("edital 01/2027")]
    [InlineData("processo-seletivo_a.b")]
    [InlineData("")]
    public void De_TextoSemAcentoEmMinusculas_DevolveOMesmoTexto(string texto) =>
        FormaParaComparacao.De(texto).Should().Be(texto);

    [Fact]
    public void De_Maiusculas_ViramMinusculas() => FormaParaComparacao.De("ABC Xyz").Should().Be("abc xyz");

    [Fact]
    public void De_FormaDecomposta_ProduzOMesmoValorDaComposta() =>
        FormaParaComparacao.De("José").Should().Be(FormaParaComparacao.De("José")).And.Be("jose");

    [Fact]
    public void De_LetraForaDaTabela_PassaIntacta() => FormaParaComparacao.De("ø ß").Should().Be("ø ß");

    [Fact]
    public void De_TextoNulo_Lanca() =>
        FluentActions.Invoking(static () => FormaParaComparacao.De(null!)).Should().Throw<ArgumentNullException>();

    [Theory(DisplayName = "Termo ausente, em branco ou só com caractere nulo não é busca")]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("\0")]
    public void Termo_SemConteudoPesquisavel_DevolveNulo(string? texto) =>
        FormaParaComparacao.Termo(texto).Should().BeNull();

    [Fact]
    public void Termo_TextoComNuloEEspacos_DescartaONuloAparaENormaliza() =>
        FormaParaComparacao.Termo("  A\0ção  ").Should().Be("acao");
}
