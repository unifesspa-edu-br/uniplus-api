namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Validators;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Configuracao.Application.Commands.TermosConsentimento;

public sealed class CriarTermoConsentimentoCommandValidatorTests
{
    private readonly CriarTermoConsentimentoCommandValidator _validator = new();

    private static CriarTermoConsentimentoCommand Base() =>
        new("Termo LGPD", "Texto do termo", "Lei 13.709/2018", "REGISTRO_DIGITAL_SEM_LOG_IP");

    [Fact(DisplayName = "Comando válido passa no validator")]
    public void Valido_Passa()
    {
        _validator.Validate(Base()).IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Comando com rascunho vazio (só nome) passa no validator")]
    public void SoNome_Passa()
    {
        _validator.Validate(Base() with { TextoRascunho = null, BaseLegalRascunho = null, FormaAceiteRascunho = null })
            .IsValid.Should().BeTrue();
    }

    [Theory(DisplayName = "Nome ausente ou em branco é rejeitado")]
    [InlineData("")]
    [InlineData("   ")]
    public void NomeVazio_Rejeita(string nome)
    {
        ValidationResult resultado = _validator.Validate(Base() with { Nome = nome });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarTermoConsentimentoCommand.Nome));
    }
}
