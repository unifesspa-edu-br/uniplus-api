namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Validators;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Configuracao.Application.Commands.CondicoesAtendimento;

/// <summary>
/// O validator antecipa a obrigatoriedade de código e nome, o formato fechado do
/// código (UPPER_SNAKE) e os limites de tamanho, mantendo a fronteira simétrica
/// com o domínio (#590).
/// </summary>
public sealed class CondicaoAtendimentoValidatorTests
{
    private readonly CriarCondicaoAtendimentoCommandValidator _validator = new();

    private static CriarCondicaoAtendimentoCommand Base() =>
        new("DISLEXIA", "Dislexia", "Transtorno específico de aprendizagem");

    [Fact(DisplayName = "Comando válido passa no validator")]
    public void Valido_Passa()
    {
        _validator.Validate(Base()).IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Comando sem descrição passa no validator")]
    public void SemDescricao_Passa()
    {
        _validator.Validate(Base() with { Descricao = null }).IsValid.Should().BeTrue();
    }

    [Theory(DisplayName = "Código ausente ou em branco é rejeitado")]
    [InlineData("")]
    [InlineData("   ")]
    public void CodigoVazio_Rejeita(string codigo)
    {
        ValidationResult resultado = _validator.Validate(Base() with { Codigo = codigo });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarCondicaoAtendimentoCommand.Codigo));
    }

    [Theory(DisplayName = "Código fora do formato fechado é rejeitado (minúsculas, dígito inicial, hífen)")]
    [InlineData("dislexia")]
    [InlineData("1PCD")]
    [InlineData("PCD-A")]
    public void CodigoFormatoInvalido_Rejeita(string codigo)
    {
        ValidationResult resultado = _validator.Validate(Base() with { Codigo = codigo });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarCondicaoAtendimentoCommand.Codigo));
    }

    [Fact(DisplayName = "Nome ausente é rejeitado")]
    public void NomeVazio_Rejeita()
    {
        ValidationResult resultado = _validator.Validate(Base() with { Nome = "  " });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarCondicaoAtendimentoCommand.Nome));
    }

    [Fact(DisplayName = "Nome acima de 200 caracteres é rejeitado")]
    public void NomeLongo_Rejeita()
    {
        ValidationResult resultado = _validator.Validate(Base() with { Nome = new string('N', 201) });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarCondicaoAtendimentoCommand.Nome));
    }

    [Fact(DisplayName = "Descrição acima de 1000 caracteres é rejeitada")]
    public void DescricaoLonga_Rejeita()
    {
        ValidationResult resultado = _validator.Validate(Base() with { Descricao = new string('D', 1001) });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarCondicaoAtendimentoCommand.Descricao));
    }
}
