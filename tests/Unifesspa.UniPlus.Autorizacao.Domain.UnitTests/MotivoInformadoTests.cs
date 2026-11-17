namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

public sealed class MotivoInformadoTests
{
    [Theory(DisplayName = "O motivo ausente, em branco ou com o caractere nulo não vale")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \t")]
    [InlineData("Uso indevido\0apontado")]
    public void Normalizar_Invalido_DevolveNulo(string? texto) =>
        MotivoInformado.Normalizar(texto).Should().BeNull();

    [Fact(DisplayName = "O motivo sai sem espaços nas pontas")]
    public void Normalizar_AparaAsPontas()
    {
        MotivoInformado? motivo = MotivoInformado.Normalizar("  Uso indevido.  ");

        motivo!.Valor.Should().Be("Uso indevido.");
        motivo.ToString().Should().Be("Uso indevido.");
    }

    [Fact(DisplayName = "O motivo vale até o máximo de caracteres e um a mais não vale")]
    public void Normalizar_Fronteira()
    {
        MotivoInformado.Normalizar(new string('a', MotivoInformado.MaximoDeCaracteres)).Should().NotBeNull();
        MotivoInformado.Normalizar(new string('a', MotivoInformado.MaximoDeCaracteres + 1)).Should().BeNull();
    }

    [Fact(DisplayName = "O motivo conta caracteres, como a coluna: o caractere fora do plano básico vale um")]
    public void Normalizar_ContaCaracteres()
    {
        string noLimite = string.Concat(Enumerable.Repeat("😀", MotivoInformado.MaximoDeCaracteres));

        MotivoInformado.Normalizar(noLimite).Should().NotBeNull();
        MotivoInformado.Normalizar(noLimite + "a").Should().BeNull();
    }

    [Fact(DisplayName = "Dois motivos com o mesmo texto são iguais")]
    public void Igualdade_PorValor() =>
        MotivoInformado.Normalizar("Incidente.").Should().Be(MotivoInformado.Normalizar(" Incidente. "));
}
