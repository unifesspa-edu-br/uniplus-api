namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Validators;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDeficiencia;

/// <summary>
/// O validator antecipa a obrigatoriedade do nome e os limites de tamanho de nome e
/// descrição, mantendo a fronteira simétrica com o domínio (UNI-REQ-0012).
/// </summary>
public sealed class TipoDeficienciaValidatorTests
{
    private readonly CriarTipoDeficienciaCommandValidator _validator = new();

    private static CriarTipoDeficienciaCommand Base() =>
        new("Visual", "Deficiência relacionada à visão");

    [Fact(DisplayName = "Comando válido passa no validator")]
    public void Valido_Passa()
    {
        _validator.Validate(Base()).IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Comando sem descrição é rejeitado (ADR-0116: descrição obrigatória)")]
    public void SemDescricao_Rejeita()
    {
        ValidationResult resultado = _validator.Validate(Base() with { Descricao = "" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarTipoDeficienciaCommand.Descricao));
    }

    [Theory(DisplayName = "Nome ausente ou em branco é rejeitado")]
    [InlineData("")]
    [InlineData("   ")]
    public void NomeVazio_Rejeita(string nome)
    {
        ValidationResult resultado = _validator.Validate(Base() with { Nome = nome });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarTipoDeficienciaCommand.Nome));
    }

    [Fact(DisplayName = "Nome acima do tamanho máximo é rejeitado")]
    public void NomeLongo_Rejeita()
    {
        ValidationResult resultado = _validator.Validate(Base() with { Nome = new string('A', 201) });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarTipoDeficienciaCommand.Nome));
    }

    [Fact(DisplayName = "Descrição acima do tamanho máximo é rejeitada")]
    public void DescricaoLonga_Rejeita()
    {
        ValidationResult resultado = _validator.Validate(Base() with { Descricao = new string('A', 1001) });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarTipoDeficienciaCommand.Descricao));
    }
}
