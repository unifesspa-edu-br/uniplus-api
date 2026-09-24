namespace Unifesspa.UniPlus.Kernel.UnitTests.Extensions;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Extensions;

public sealed class TextoNormalizavelTests
{
    // Montados em tempo de execução: o xUnit troca o surrogate sem par de um InlineData pelo
    // caractere de substituição antes de o teste rodar.
    private static readonly string NaoCaractere = "Res. 805" + (char)0xFFFE;
    private static readonly string SurrogateSemPar = "Res. 805" + (char)0xD800;

    [Fact(DisplayName = "TentarNormalizar recusa não-caractere e surrogate sem par sem lançar")]
    public void TentarNormalizar_TextoNaoNormalizavel_Falso()
    {
        TextoNormalizavel.TentarNormalizar(NaoCaractere, out _).Should().BeFalse();
        TextoNormalizavel.TentarNormalizar(SurrogateSemPar, out _).Should().BeFalse();
    }

    [Fact(DisplayName = "TentarNfc recusa não-caractere como caractere inválido")]
    public void TentarNfc_NaoCaractere_CaractereInvalido() =>
        TextoNormalizavel.TentarNfc(NaoCaractere, 40, out _).Should().Be(SituacaoDoTexto.CaractereInvalido);

    [Fact(DisplayName = "TentarNfc recusa pelo teto, antes de varrer, o texto grande demais")]
    public void TentarNfc_TextoMaiorQueOTeto_TamanhoExcedido() =>
        TextoNormalizavel.TentarNfc(new string('R', 1_000_000) + "\n", 40, out _)
            .Should().Be(SituacaoDoTexto.TamanhoExcedido, "a quebra de linha no fim daria caractere inválido se o texto fosse varrido");

    [Fact(DisplayName = "TentarNfc aceita o texto decomposto que cabe em NFC e devolve a forma normalizada")]
    public void TentarNfc_DecompostoQueCabe_Valido()
    {
        // U+1F86 decompõe em quatro caracteres: 160 recebidos, 40 em NFC.
        string decomposto = string.Concat(Enumerable.Repeat("\u03B1\u0313\u0342\u0345", 40));

        TextoNormalizavel.TentarNfc(decomposto, 40, out string normalizado).Should().Be(SituacaoDoTexto.Valido);
        normalizado.Should().Be(new string('\u1F86', 40));
    }

    [Fact(DisplayName = "TentarNfc recusa o texto que passa do limite depois do NFC")]
    public void TentarNfc_PassaDoLimiteEmNfc_TamanhoExcedido() =>
        TextoNormalizavel.TentarNfc(new string('b', 39) + "\u0344", 40, out _).Should().Be(SituacaoDoTexto.TamanhoExcedido);

    [Fact(DisplayName = "TentarNfcDeTextoLivre aceita tabulação e quebra de linha e recusa o caractere nulo")]
    public void TentarNfcDeTextoLivre_RecusaSoONulo()
    {
        TextoNormalizavel.TentarNfcDeTextoLivre("art. 5º,\tinciso II\nparágrafo", 40, out _).Should().Be(SituacaoDoTexto.Valido);
        TextoNormalizavel.TentarNfcDeTextoLivre("art. 5º\u0000", 40, out _).Should().Be(SituacaoDoTexto.CaractereInvalido);
    }
}
