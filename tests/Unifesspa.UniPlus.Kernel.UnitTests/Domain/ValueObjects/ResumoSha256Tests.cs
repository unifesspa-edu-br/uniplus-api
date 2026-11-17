namespace Unifesspa.UniPlus.Kernel.UnitTests.Domain.ValueObjects;

using System.Text;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

public sealed class ResumoSha256Tests
{
    private const string ResumoDeTeste = "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";

    [Fact(DisplayName = "O resumo de um conteúdo é o SHA-256 dele em hexadecimal minúsculo")]
    public void DoConteudo_CalculaOSha256EmMinusculas() =>
        ResumoSha256.DoConteudo(Encoding.UTF8.GetBytes("test")).Valor.Should().Be(ResumoDeTeste);

    [Fact(DisplayName = "O texto com a forma de um SHA-256 vira resumo e volta igual")]
    public void De_TextoNoFormato_DevolveOMesmoTexto() =>
        ResumoSha256.De(ResumoDeTeste).ToString().Should().Be(ResumoDeTeste);

    [Theory(DisplayName = "O texto que não é hexadecimal minúsculo de 64 caracteres falha alto")]
    [InlineData("")]
    [InlineData("9f86d081")]
    [InlineData("9F86D081884C7D659A2FEAA0C55AD015A3BF4F1B2B0B822CD15D6C15B0F00A08")]
    [InlineData("9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a0g")]
    [InlineData("9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08a")]
    [InlineData("9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08\n")]
    public void De_TextoForaDoFormato_Lanca(string texto)
    {
        Action criar = () => ResumoSha256.De(texto);

        criar.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "O texto nulo falha alto")]
    public void De_Nulo_Lanca()
    {
        Action criar = () => ResumoSha256.De(null!);

        criar.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Dois resumos do mesmo texto são iguais")]
    public void Igualdade_PorValor() =>
        ResumoSha256.De(ResumoDeTeste).Should().Be(ResumoSha256.DoConteudo(Encoding.UTF8.GetBytes("test")));
}
