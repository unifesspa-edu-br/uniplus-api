namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

public sealed class DefinirProvaPresencialCommandValidatorTests
{
    private readonly DefinirProvaPresencialCommandValidator _validator = new();

    [Fact]
    public void Validate_IdentidadeVazia_Recusa() =>
        _validator.Validate(new DefinirProvaPresencialCommand(Guid.Empty, false, []))
            .Errors.Should().Contain(e => e.PropertyName == "ProcessoSeletivoId");

    [Fact]
    public void Validate_ElementoNulo_DeixaValidacaoAoDominio() =>
        _validator.Validate(new DefinirProvaPresencialCommand(Guid.NewGuid(), true, [null!]))
            .IsValid.Should().BeTrue();

    [Fact]
    public void Validate_MunicipioInvalido_DeixaValidacaoAoDominio() =>
        _validator.Validate(new DefinirProvaPresencialCommand(Guid.NewGuid(), true, [new("150420", "SP")]))
            .IsValid.Should().BeTrue();
}
