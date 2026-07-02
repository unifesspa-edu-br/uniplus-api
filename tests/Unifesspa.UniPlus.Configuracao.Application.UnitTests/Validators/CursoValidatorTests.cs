namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Validators;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Configuracao.Application.Commands.Cursos;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>
/// O validator antecipa a obrigatoriedade de código, nome, grau e nível de
/// ensino, os comprimentos máximos e o domínio fechado do grupo de área do ENEM
/// (opcional), mantendo a fronteira simétrica com o domínio (#748).
/// </summary>
public sealed class CursoValidatorTests
{
    private readonly CriarCursoCommandValidator _validator = new();

    private static CriarCursoCommand Base() =>
        new("ENG_CIVIL", "Engenharia Civil", "Bacharelado", "Graduação", GrupoCurso.Tecnologica);

    [Fact(DisplayName = "Comando válido passa no validator")]
    public void Valido_Passa()
    {
        _validator.Validate(Base()).IsValid.Should().BeTrue();
    }

    [Theory(DisplayName = "Comando sem grupo de área do ENEM (nulo ou em branco) passa no validator")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SemGrupoAreaEnem_Passa(string? grupoAreaEnem)
    {
        _validator.Validate(Base() with { GrupoAreaEnem = grupoAreaEnem })
            .IsValid.Should().BeTrue();
    }

    [Theory(DisplayName = "Código ausente ou em branco é rejeitado")]
    [InlineData("")]
    [InlineData("   ")]
    public void CodigoVazio_Rejeita(string codigo)
    {
        ValidationResult resultado = _validator.Validate(Base() with { Codigo = codigo });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarCursoCommand.Codigo));
    }

    [Fact(DisplayName = "Código acima de 60 caracteres é rejeitado")]
    public void CodigoLongo_Rejeita()
    {
        ValidationResult resultado = _validator.Validate(Base() with { Codigo = new string('A', 61) });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarCursoCommand.Codigo));
    }

    [Fact(DisplayName = "Nome ausente é rejeitado")]
    public void NomeVazio_Rejeita()
    {
        ValidationResult resultado = _validator.Validate(Base() with { Nome = "  " });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarCursoCommand.Nome));
    }

    [Fact(DisplayName = "Grau ausente é rejeitado")]
    public void GrauVazio_Rejeita()
    {
        ValidationResult resultado = _validator.Validate(Base() with { Grau = "  " });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarCursoCommand.Grau));
    }

    [Fact(DisplayName = "Nível de ensino ausente é rejeitado")]
    public void NivelEnsinoVazio_Rejeita()
    {
        ValidationResult resultado = _validator.Validate(Base() with { NivelEnsino = "  " });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarCursoCommand.NivelEnsino));
    }

    [Theory(DisplayName = "Grupo de área do ENEM fora do domínio fechado é rejeitado")]
    [InlineData("Exatas")]
    [InlineData("Tecnologica")]
    [InlineData("HUMANÍSTICA I")]
    public void GrupoAreaEnemInvalido_Rejeita(string grupoAreaEnem)
    {
        ValidationResult resultado = _validator.Validate(Base() with { GrupoAreaEnem = grupoAreaEnem });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarCursoCommand.GrupoAreaEnem));
    }

    [Theory(DisplayName = "Cada um dos quatro grupos canônicos é aceito")]
    [InlineData(GrupoCurso.Tecnologica)]
    [InlineData(GrupoCurso.HumanisticaI)]
    [InlineData(GrupoCurso.HumanisticaII)]
    [InlineData(GrupoCurso.SaudeEBiologicas)]
    public void GrupoAreaEnemCanonico_Aceita(string grupoAreaEnem)
    {
        _validator.Validate(Base() with { GrupoAreaEnem = grupoAreaEnem })
            .IsValid.Should().BeTrue();
    }
}
