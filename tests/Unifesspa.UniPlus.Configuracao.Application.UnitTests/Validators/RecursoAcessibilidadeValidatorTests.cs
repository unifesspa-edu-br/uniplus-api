namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Validators;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Configuracao.Application.Commands.RecursosAcessibilidade;

/// <summary>
/// O validator antecipa a obrigatoriedade do nome e os limites de tamanho de nome
/// e descrição, mantendo a fronteira simétrica com o domínio (#590).
/// </summary>
public sealed class RecursoAcessibilidadeValidatorTests
{
    private readonly CriarRecursoAcessibilidadeCommandValidator _validator = new();

    private static CriarRecursoAcessibilidadeCommand Base() =>
        new("Ledor", "Profissional que lê a prova ao candidato");

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

    [Theory(DisplayName = "Nome ausente ou em branco é rejeitado")]
    [InlineData("")]
    [InlineData("   ")]
    public void NomeVazio_Rejeita(string nome)
    {
        ValidationResult resultado = _validator.Validate(Base() with { Nome = nome });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarRecursoAcessibilidadeCommand.Nome));
    }

    [Fact(DisplayName = "Nome acima de 200 caracteres é rejeitado")]
    public void NomeLongo_Rejeita()
    {
        ValidationResult resultado = _validator.Validate(Base() with { Nome = new string('A', 201) });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarRecursoAcessibilidadeCommand.Nome));
    }

    [Fact(DisplayName = "Descrição acima de 1000 caracteres é rejeitada")]
    public void DescricaoLonga_Rejeita()
    {
        ValidationResult resultado = _validator.Validate(Base() with { Descricao = new string('D', 1001) });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarRecursoAcessibilidadeCommand.Descricao));
    }
}
