namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

public sealed class DefinirImportacaoDoResultadoCommandValidatorTests
{
    private readonly DefinirImportacaoDoResultadoCommandValidator _validator = new();

    [Fact]
    public void Validate_IdentidadeVazia_Recusa() =>
        _validator.Validate(new DefinirImportacaoDoResultadoCommand(Guid.Empty, [], null))
            .Errors.Should().Contain(e => e.PropertyName == "ProcessoSeletivoId");

    [Fact]
    public void Validate_RelatorioNulo_DeixaValidacaoAoDominio() =>
        _validator.Validate(new DefinirImportacaoDoResultadoCommand(Guid.NewGuid(), [null!], null)).IsValid.Should().BeTrue();

    [Theory]
    [InlineData("MEU LEIAUTE", "v1")]
    [InlineData("LEIAUTÉ", "v1")]
    [InlineData("A", "versao-com-mais-de-dezesseis-caracteres")]
    [InlineData("", "")]
    public void Validate_FormaDoLeiaute_DeixaValidacaoAoDominio(string codigo, string versao) =>
        _validator.Validate(new DefinirImportacaoDoResultadoCommand(Guid.NewGuid(), [new(codigo, versao)], null)).IsValid.Should().BeTrue();
}
