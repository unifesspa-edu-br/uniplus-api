namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Validators;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Configuracao.Application.Commands.PesosAreaEnem;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>
/// O validator antecipa a obrigatoriedade da resolução, o domínio fechado do
/// grupo de área, a não-negatividade dos cinco pesos e do corte de redação e a
/// presença da base legal, mantendo a fronteira simétrica com o domínio (#594).
/// </summary>
public sealed class PesoAreaEnemValidatorTests
{
    private readonly CriarPesoAreaEnemCommandValidator _validator = new();

    private static CriarPesoAreaEnemCommand Base() =>
        new("Res. 805/2024", GrupoCurso.Tecnologica, 1.50m, 1.00m, 1.00m, 1.00m, 2.00m, 400m, "Res. 805/2024 Anexo I");

    [Fact(DisplayName = "Comando válido passa no validator")]
    public void Valido_Passa()
    {
        _validator.Validate(Base()).IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Corte de redação omitido (null) passa no validator")]
    public void CorteOmitido_Passa()
    {
        _validator.Validate(Base() with { CorteRedacao = null }).IsValid.Should().BeTrue();
    }

    [Theory(DisplayName = "Resolução ausente ou em branco é rejeitada")]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolucaoVazia_Rejeita(string resolucao)
    {
        ValidationResult resultado = _validator.Validate(Base() with { Resolucao = resolucao });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarPesoAreaEnemCommand.Resolucao));
    }

    [Theory(DisplayName = "Grupo fora do domínio é rejeitado")]
    [InlineData("Engenharias")]
    [InlineData("Humanística III")]
    [InlineData("")]
    public void GrupoInvalido_Rejeita(string grupo)
    {
        ValidationResult resultado = _validator.Validate(Base() with { GrupoCurso = grupo });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarPesoAreaEnemCommand.GrupoCurso));
    }

    [Theory(DisplayName = "Peso negativo é rejeitado pelo validator (um por área)")]
    [InlineData(-1.0, 1, 1, 1, 1, nameof(CriarPesoAreaEnemCommand.PesoRedacao))]
    [InlineData(1, -1.0, 1, 1, 1, nameof(CriarPesoAreaEnemCommand.PesoCienciasNatureza))]
    [InlineData(1, 1, -1.0, 1, 1, nameof(CriarPesoAreaEnemCommand.PesoCienciasHumanas))]
    [InlineData(1, 1, 1, -1.0, 1, nameof(CriarPesoAreaEnemCommand.PesoLinguagens))]
    [InlineData(1, 1, 1, 1, -1.0, nameof(CriarPesoAreaEnemCommand.PesoMatematica))]
    public void PesoNegativo_Rejeita(decimal redacao, decimal cn, decimal ch, decimal lc, decimal mt, string propriedade)
    {
        ValidationResult resultado = _validator.Validate(Base() with
        {
            PesoRedacao = redacao,
            PesoCienciasNatureza = cn,
            PesoCienciasHumanas = ch,
            PesoLinguagens = lc,
            PesoMatematica = mt,
        });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == propriedade);
    }

    [Fact(DisplayName = "Corte de redação negativo é rejeitado")]
    public void CorteNegativo_Rejeita()
    {
        ValidationResult resultado = _validator.Validate(Base() with { CorteRedacao = -1.000m });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarPesoAreaEnemCommand.CorteRedacao));
    }

    [Fact(DisplayName = "Peso zero é aceito")]
    public void PesoZero_Aceita()
    {
        _validator.Validate(Base() with { PesoCienciasHumanas = 0m }).IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Base legal ausente é rejeitada")]
    public void BaseLegalVazia_Rejeita()
    {
        ValidationResult resultado = _validator.Validate(Base() with { BaseLegal = "  " });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarPesoAreaEnemCommand.BaseLegal));
    }
}
