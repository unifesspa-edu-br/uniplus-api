namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

public sealed class DefinirCadastroDeReservaCommandValidatorTests
{
    private readonly DefinirCadastroDeReservaCommandValidator _validator = new();

    [Fact]
    public void Validate_IdentidadeVazia_Recusa() =>
        _validator.Validate(new DefinirCadastroDeReservaCommand(Guid.Empty, []))
            .Errors.Should().Contain(e => e.PropertyName == "ProcessoSeletivoId");

    [Fact]
    public void Validate_ListaNula_Recusa() =>
        _validator.Validate(new DefinirCadastroDeReservaCommand(Guid.NewGuid(), null!))
            .Errors.Should().Contain(e => e.PropertyName == "Chamadas");

    [Fact]
    public void Validate_ListaVazia_EValida() =>
        _validator.Validate(new DefinirCadastroDeReservaCommand(Guid.NewGuid(), [])).IsValid.Should().BeTrue();

    [Fact]
    public void Validate_ElementoNuloEQuantidadeNegativa_DeixaValidacaoAoDominio() =>
        _validator.Validate(new DefinirCadastroDeReservaCommand(Guid.NewGuid(), [null, new(1, -1)])).IsValid.Should().BeTrue();
}
