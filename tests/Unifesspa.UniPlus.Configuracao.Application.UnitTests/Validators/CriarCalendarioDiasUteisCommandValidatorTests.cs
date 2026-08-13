namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Validators;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Configuracao.Application.Commands.CalendariosDiasUteis;

public sealed class CriarCalendarioDiasUteisCommandValidatorTests
{
    private readonly CriarCalendarioDiasUteisCommandValidator _validator = new();

    private static CriarCalendarioDiasUteisCommand Base() =>
        new(
            "2027.1",
            [new DiaNaoUtilCommandItem("NACIONAL", null, null, null, new DateOnly(2027, 1, 1), "Confraternização Universal")]);

    [Fact(DisplayName = "Comando válido passa no validator")]
    public void Valido_Passa()
    {
        _validator.Validate(Base()).IsValid.Should().BeTrue();
    }

    [Theory(DisplayName = "Versão do dataset ausente ou em branco é rejeitada")]
    [InlineData("")]
    [InlineData("   ")]
    public void VersaoDatasetVazia_Rejeita(string versaoDataset)
    {
        ValidationResult resultado = _validator.Validate(Base() with { VersaoDataset = versaoDataset });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarCalendarioDiasUteisCommand.VersaoDataset));
    }

    [Fact(DisplayName = "Versão do dataset acima de 60 caracteres é rejeitada")]
    public void VersaoDatasetMuitoLonga_Rejeita()
    {
        ValidationResult resultado = _validator.Validate(Base() with { VersaoDataset = new string('A', 61) });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarCalendarioDiasUteisCommand.VersaoDataset));
    }

    [Fact(DisplayName = "Lista de dias não úteis vazia é rejeitada")]
    public void DiasNaoUteisVazia_Rejeita()
    {
        ValidationResult resultado = _validator.Validate(Base() with { DiasNaoUteis = [] });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarCalendarioDiasUteisCommand.DiasNaoUteis));
    }

    [Fact(DisplayName = "Item nulo na lista de dias não úteis é rejeitado")]
    public void ItemNulo_Rejeita()
    {
        ValidationResult resultado = _validator.Validate(Base() with
        {
            DiasNaoUteis = [new DiaNaoUtilCommandItem("NACIONAL", null, null, null, new DateOnly(2027, 1, 1), "Válido"), null!],
        });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName.StartsWith(
            nameof(CriarCalendarioDiasUteisCommand.DiasNaoUteis), StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Item com abrangência vazia é rejeitado")]
    public void ItemComAbrangenciaVazia_Rejeita()
    {
        ValidationResult resultado = _validator.Validate(Base() with
        {
            DiasNaoUteis = [new DiaNaoUtilCommandItem("", null, null, null, new DateOnly(2027, 1, 1), "Descrição válida")],
        });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName.EndsWith(
            nameof(DiaNaoUtilCommandItem.Abrangencia), StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Item com descrição vazia é rejeitado")]
    public void ItemComDescricaoVazia_Rejeita()
    {
        ValidationResult resultado = _validator.Validate(Base() with
        {
            DiasNaoUteis = [new DiaNaoUtilCommandItem("NACIONAL", null, null, null, new DateOnly(2027, 1, 1), "")],
        });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName.EndsWith(
            nameof(DiaNaoUtilCommandItem.Descricao), StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Item com descrição acima de 200 caracteres é rejeitado")]
    public void ItemComDescricaoMuitoLonga_Rejeita()
    {
        ValidationResult resultado = _validator.Validate(Base() with
        {
            DiasNaoUteis = [new DiaNaoUtilCommandItem("NACIONAL", null, null, null, new DateOnly(2027, 1, 1), new string('A', 201))],
        });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName.EndsWith(
            nameof(DiaNaoUtilCommandItem.Descricao), StringComparison.Ordinal));
    }
}
