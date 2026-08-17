namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Validators;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Só a forma do <c>ProcessoSeletivoId</c> — identificador de rota sem equivalente no
/// agregado. Tamanho de Título/TermoAceiteTexto tem equivalente de domínio (ADR-0125) e
/// é coberto em <c>ProcessoSeletivoSessaoEditorialTests</c>.
/// </summary>
public sealed class DefinirFormularioCommandValidatorTests
{
    private static readonly DefinirFormularioCommandValidator Validator = new();

    [Fact(DisplayName = "Passa com título e termo nulos — ausência é estado válido")]
    public void Aceita_TituloETermoNulos()
    {
        ValidationResult result = Validator.Validate(new DefinirFormularioCommand(Guid.CreateVersion7(), null, null, PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Falha quando ProcessoSeletivoId é vazio")]
    public void Rejeita_ProcessoSeletivoIdVazio()
    {
        ValidationResult result = Validator.Validate(new DefinirFormularioCommand(Guid.Empty, null, null, PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ProcessoSeletivoId");
    }
}
