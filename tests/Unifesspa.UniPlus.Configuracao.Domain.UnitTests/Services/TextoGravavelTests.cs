namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Services;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>O texto que o banco grava — sem caractere nulo e em Unicode válido.</summary>
public sealed class TextoGravavelTests
{
    [Theory(DisplayName = "O texto gravável não tem caractere nulo e é Unicode válido")]
    [InlineData("", true)]
    [InlineData("Habilitação Medicina", true)]
    [InlineData("com\0nulo", false)]
    public void EhGravavel_ConfereCaractereNuloEUnicode(string texto, bool gravavel) =>
        TextoGravavel.EhGravavel(texto).Should().Be(gravavel);

    [Fact(DisplayName = "O surrogate sem par não é Unicode válido")]
    public void EhGravavel_SurrogateSemPar_NaoEGravavel() => TextoGravavel.EhGravavel(new string('\uD800', 1)).Should().BeFalse();

    [Fact(DisplayName = "Texto ausente é recusado como argumento nulo, e não como não gravável")]
    public void EhGravavel_TextoNulo_Lanca() =>
        FluentActions.Invoking(static () => TextoGravavel.EhGravavel(null!)).Should().Throw<ArgumentNullException>();

    [Fact(DisplayName = "O texto não gravável é recusado no campo dele, com o código de texto não gravável")]
    public void Recusar_TextoNaoGravavel_RecusaNoCampo()
    {
        FieldError recusa = TextoGravavel.Recusar("nome", "a\0").Should().ContainSingle().Subject;

        recusa.Field.Should().Be("nome");
        recusa.Error.Code.Should().Be(ModeloFormularioErrorCodes.TextoNaoGravavel);
    }

    [Theory(DisplayName = "O texto gravável ou ausente não é recusado")]
    [InlineData("Habilitação")]
    [InlineData(null)]
    public void Recusar_TextoGravavelOuAusente_SemRecusa(string? texto) => TextoGravavel.Recusar("nome", texto).Should().BeEmpty();

}
