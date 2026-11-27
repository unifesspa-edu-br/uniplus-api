namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Validators;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

public sealed class CondicaoGatilhoInputValidatorTests
{
    private static ValidationResult Validar(int? clausula) =>
        new CondicaoGatilhoInputValidator().Validate(new CondicaoGatilhoInput(clausula, "MODALIDADE", "IGUAL", "AC"));

    [Fact(DisplayName = "A cláusula ausente é recusada com a mensagem de cláusula obrigatória")]
    public void ClausulaAusente_Recusa()
    {
        ValidationResult resultado = Validar(null);

        resultado.Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("Informe a cláusula da condição.");
    }

    [Theory(DisplayName = "A cláusula informada é aceita pelo validador, inclusive zero e a negativa, que é do agregado")]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-1)]
    public void ClausulaInformada_Aceita(int clausula) =>
        Validar(clausula).IsValid.Should().BeTrue();
}
