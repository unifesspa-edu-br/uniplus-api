namespace Unifesspa.UniPlus.Kernel.UnitTests.Extensions;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Extensions;

public sealed class CaracteresInvisiveisTests
{
    [Theory(DisplayName = "Contem acusa controle, formatação e separadores de linha e de parágrafo")]
    [InlineData("805\u0000")]
    [InlineData("Res.\n805/2024")]
    [InlineData("Res.\t805/2024")]
    [InlineData("Res. \u202E4202/508")]
    [InlineData("Res.\u200B805")]
    [InlineData("Res.\u2028805")]
    [InlineData("Res.\u2029805")]
    [InlineData("Res.\U000E0041805")]
    public void Contem_TextoComCaractereInvisivel_Verdadeiro(string texto) =>
        CaracteresInvisiveis.Contem(texto).Should().BeTrue();

    [Fact(DisplayName = "Contem acusa surrogate sem par, alto ou baixo")]
    public void Contem_SurrogateSemPar_Verdadeiro()
    {
        // Montado em tempo de execução: o xUnit troca o surrogate sem par de um InlineData
        // pelo caractere de substituição antes de o teste rodar.
        CaracteresInvisiveis.Contem("Res." + (char)0xD800 + "805").Should().BeTrue();
        CaracteresInvisiveis.Contem("Res." + (char)0xDC00).Should().BeTrue();
    }

    [Theory(DisplayName = "Contem aceita texto visível, com acento composto ou decomposto, espaço comum e caractere fora do plano básico")]
    [InlineData("")]
    [InlineData("Resolução nº 805/2024/Consepe – Anexo I")]
    [InlineData("Resoluc\u0327a\u0303o 805")]
    [InlineData("Res. 805 \U0001F4D8")]
    public void Contem_TextoVisivel_Falso(string texto) =>
        CaracteresInvisiveis.Contem(texto).Should().BeFalse();

    [Fact(DisplayName = "Substituir troca cada caractere invisível, inclusive fora do plano básico e surrogate sem par, e mantém o resto")]
    public void Substituir_TrocaSoOsInvisiveis()
    {
        string texto = "Res.\n805\u202E/\U000E0041" + (char)0xD800 + "2024 \U0001F4D8 ç";

        CaracteresInvisiveis.Substituir(texto, '?').Should().Be("Res.?805?/??2024 \U0001F4D8 ç");
    }

    [Fact(DisplayName = "ParaEco limita o texto sem partir par substituto e troca os invisíveis")]
    public void ParaEco_LimitaESaneia()
    {
        CaracteresInvisiveis.ParaEco("AB\nCD", 40).Should().Be("AB?CD");
        CaracteresInvisiveis.ParaEco(new string('x', 39) + "\U0001F4D8" + "yz", 40).Should().Be(new string('x', 39) + "…");
    }

    [Theory(DisplayName = "ParaEco recusa tamanho máximo zero ou negativo")]
    [InlineData(0)]
    [InlineData(-1)]
    public void ParaEco_TamanhoInvalido_Lanca(int tamanho)
    {
        Action eco = () => CaracteresInvisiveis.ParaEco("texto", tamanho);

        eco.Should().Throw<ArgumentOutOfRangeException>();
    }
}
