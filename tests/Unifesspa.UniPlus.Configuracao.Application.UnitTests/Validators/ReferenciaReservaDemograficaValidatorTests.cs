namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Validators;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Configuracao.Application.Commands.ReferenciasReservaDemografica;

/// <summary>
/// O validator antecipa o intervalo dos três percentuais (0–100), a presença do
/// Censo e da base legal, mantendo a fronteira simétrica com o domínio (#593).
/// </summary>
public sealed class ReferenciaReservaDemograficaValidatorTests
{
    private readonly CriarReferenciaReservaDemograficaCommandValidator _validator = new();

    private static CriarReferenciaReservaDemograficaCommand Base() =>
        new("2022", 78.50m, 1.20m, 8.40m, "Lei 12.711/2012, art. 10, III");

    [Fact(DisplayName = "Comando válido passa no validator")]
    public void Valido_Passa()
    {
        _validator.Validate(Base()).IsValid.Should().BeTrue();
    }

    [Theory(DisplayName = "Censo ausente ou em branco é rejeitado")]
    [InlineData("")]
    [InlineData("   ")]
    public void CensoVazio_Rejeita(string censo)
    {
        ValidationResult resultado = _validator.Validate(Base() with { CensoReferencia = censo });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarReferenciaReservaDemograficaCommand.CensoReferencia));
    }

    [Fact(DisplayName = "Base legal ausente é rejeitada")]
    public void BaseLegalVazia_Rejeita()
    {
        ValidationResult resultado = _validator.Validate(Base() with { BaseLegal = "  " });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarReferenciaReservaDemograficaCommand.BaseLegal));
    }

    [Theory(DisplayName = "Percentual fora do intervalo é rejeitado pelo validator")]
    [InlineData(-0.01, 10, 10, nameof(CriarReferenciaReservaDemograficaCommand.PpiPercentual))]
    [InlineData(100.01, 10, 10, nameof(CriarReferenciaReservaDemograficaCommand.PpiPercentual))]
    [InlineData(10, -1, 10, nameof(CriarReferenciaReservaDemograficaCommand.QuilombolaPercentual))]
    [InlineData(10, 10, 150, nameof(CriarReferenciaReservaDemograficaCommand.PcdPercentual))]
    public void PercentualForaDeFaixa_Rejeita(decimal ppi, decimal quilombola, decimal pcd, string propriedade)
    {
        ValidationResult resultado = _validator.Validate(
            Base() with { PpiPercentual = ppi, QuilombolaPercentual = quilombola, PcdPercentual = pcd });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == propriedade);
    }

    [Theory(DisplayName = "Percentual no limite (0 e 100) é aceito")]
    [InlineData(0)]
    [InlineData(100)]
    public void PercentualNoLimite_Aceita(decimal valor)
    {
        _validator.Validate(Base() with { PpiPercentual = valor, QuilombolaPercentual = valor, PcdPercentual = valor })
            .IsValid.Should().BeTrue();
    }
}
