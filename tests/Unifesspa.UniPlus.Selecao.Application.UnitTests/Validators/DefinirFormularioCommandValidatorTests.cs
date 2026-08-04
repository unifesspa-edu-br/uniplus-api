namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Validators;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class DefinirFormularioCommandValidatorTests
{
    private static readonly DefinirFormularioCommandValidator Validator = new();

    [Fact(DisplayName = "Passa com título e termo nulos — ausência é estado válido")]
    public void Aceita_TituloETermoNulos()
    {
        ValidationResult result = Validator.Validate(new DefinirFormularioCommand(Guid.CreateVersion7(), null, null, PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Falha quando o título excede 300 caracteres — mesmo limite da coluna, recusado antes do SaveChanges")]
    public void Rejeita_TituloExcedeLimite()
    {
        ValidationResult result = Validator.Validate(new DefinirFormularioCommand(
            Guid.CreateVersion7(), new string('a', 301), null, PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Titulo");
    }

    [Fact(DisplayName = "Falha quando o termo de aceite excede 4000 caracteres — mesmo limite da coluna, recusado antes do SaveChanges")]
    public void Rejeita_TermoAceiteExcedeLimite()
    {
        ValidationResult result = Validator.Validate(new DefinirFormularioCommand(
            Guid.CreateVersion7(), null, new string('a', 4001), PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "TermoAceiteTexto");
    }
}
