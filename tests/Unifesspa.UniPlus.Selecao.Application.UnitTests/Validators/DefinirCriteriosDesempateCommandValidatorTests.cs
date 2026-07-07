namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Validators;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

public sealed class DefinirCriteriosDesempateCommandValidatorTests
{
    [Fact(DisplayName = "Validator passa com lista de critérios vazia (dimensão opcional)")]
    public void Aceita_ListaVazia()
    {
        ValidationResult result = new DefinirCriteriosDesempateCommandValidator()
            .Validate(new DefinirCriteriosDesempateCommand(Guid.CreateVersion7(), []));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Validator falha quando Criterios é nulo (payload malformado)")]
    public void Rejeita_CriteriosNulo()
    {
        ValidationResult result = new DefinirCriteriosDesempateCommandValidator()
            .Validate(new DefinirCriteriosDesempateCommand(Guid.CreateVersion7(), null!));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Criterios");
    }

    [Fact(DisplayName = "Validator falha quando Ordem não é maior que zero")]
    public void Rejeita_OrdemInvalida()
    {
        ValidationResult result = new DefinirCriteriosDesempateCommandValidator().Validate(
            new DefinirCriteriosDesempateCommand(
                Guid.CreateVersion7(),
                [new CriterioDesempateInput(0, "DESEMPATE-MAIOR-IDADE", "v1", null, null, null, null, null)]));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Criterios[0].Ordem");
    }
}
