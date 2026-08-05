namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Services;

/// <summary>
/// Vetores normativos e de borda da regra <see cref="RegrasDeNomeAbreviado.Vigente"/>
/// (ADR-0082, issue #563) — fechados em vetores porque a ADR só dá um exemplo, e o
/// identificador congelado nunca pode ter seu significado reinterpretado.
/// </summary>
public sealed class RegrasDeNomeAbreviadoTests
{
    [Theory(DisplayName = "Abreviar aplica os vetores normativos da ADR-0082")]
    [InlineData("Maria Lima Almeida", "M. L. Almeida")]
    [InlineData("Maria de Lima Almeida", "M. L. Almeida")]
    [InlineData("João dos Santos", "J. Santos")]
    [InlineData("Ana-Clara Silva", "A. Silva")]
    [InlineData("Álvaro D'Ávila", "Á. D'Ávila")]
    [InlineData("  Maria   Lima  ", "M. Lima")]
    [InlineData("Iracema", "I.")]
    [InlineData("Da Silva", "D. Silva")]
    [InlineData("Maria Silva de", "M. Silva")]
    [InlineData("Maria de", "M.")]
    public void Abreviar_AplicaOsVetoresNormativos(string nome, string esperado)
    {
        Result<string> resultado = RegrasDeNomeAbreviado.Abreviar(RegrasDeNomeAbreviado.Vigente, nome);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value.Should().Be(esperado);
    }

    [Fact(DisplayName = "Partículas no meio do nome são omitidas mesmo com mais de um primeiro nome")]
    public void Abreviar_MultiplasParticulas()
    {
        Result<string> resultado = RegrasDeNomeAbreviado.Abreviar(RegrasDeNomeAbreviado.Vigente, "Pedro de Souza e Silva Neto");

        resultado.IsSuccess.Should().BeTrue();
        // primeiros nomes não-partícula: Pedro, Souza, Silva (Neto é o sobrenome; "de" e "e" são partículas)
        resultado.Value.Should().Be("P. S. S. Neto");
    }

    [Fact(DisplayName = "Nome com identificador desconhecido é recusado, nunca resolvido pela regra vigente")]
    public void Abreviar_IdentificadorDesconhecido_Falha()
    {
        Result<string> resultado = RegrasDeNomeAbreviado.Abreviar("regra_do_futuro", "Maria Lima Almeida");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegrasDeNomeAbreviado.IdentificadorDesconhecido");
    }

    [Theory(DisplayName = "Nome vazio ou só espaços é recusado")]
    [InlineData("")]
    [InlineData("   ")]
    public void Abreviar_NomeEmBranco_Falha(string nome)
    {
        Result<string> resultado = RegrasDeNomeAbreviado.Abreviar(RegrasDeNomeAbreviado.Vigente, nome);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegrasDeNomeAbreviado.NomeEmBranco");
    }

    [Fact(DisplayName = "EhConhecida reconhece só o identificador vigente")]
    public void EhConhecida_ReconheceSoOVigente()
    {
        RegrasDeNomeAbreviado.EhConhecida(RegrasDeNomeAbreviado.Vigente).Should().BeTrue();
        RegrasDeNomeAbreviado.EhConhecida("qualquer_outro").Should().BeFalse();
    }
}
