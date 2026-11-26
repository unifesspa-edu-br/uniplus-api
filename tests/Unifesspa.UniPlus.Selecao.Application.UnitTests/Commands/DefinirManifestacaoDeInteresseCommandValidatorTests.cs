namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

public sealed class DefinirManifestacaoDeInteresseCommandValidatorTests
{
    private readonly DefinirManifestacaoDeInteresseCommandValidator _validator = new();

    [Fact]
    public void Validate_IdentidadeVazia_Recusa() =>
        _validator.Validate(new DefinirManifestacaoDeInteresseCommand(Guid.Empty, "UMA_VEZ"))
            .Errors.Should().Contain(e => e.PropertyName == "ProcessoSeletivoId");

    [Fact]
    public void Validate_SemCodigo_EValido() =>
        _validator.Validate(new DefinirManifestacaoDeInteresseCommand(Guid.NewGuid(), null)).IsValid.Should().BeTrue();

    [Fact]
    public void Validate_CodigoDesconhecido_DeixaAConferenciaAoDominio() =>
        _validator.Validate(new DefinirManifestacaoDeInteresseCommand(Guid.NewGuid(), "OUTRA")).IsValid.Should().BeTrue();
}
