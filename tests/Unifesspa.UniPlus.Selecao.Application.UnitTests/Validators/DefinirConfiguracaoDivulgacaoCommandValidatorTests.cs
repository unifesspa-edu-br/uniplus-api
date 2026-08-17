namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Validators;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Só a forma do <c>ProcessoSeletivoId</c> — identificador de rota sem equivalente no
/// agregado. Vocabulário/piso/exclusividade/justificativa têm equivalente de domínio
/// (ADR-0125) e são cobertos em
/// <c>Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ConfiguracaoDivulgacaoTests</c>.
/// </summary>
public sealed class DefinirConfiguracaoDivulgacaoCommandValidatorTests
{
    private static readonly DefinirConfiguracaoDivulgacaoCommandValidator Validator = new();

    [Fact(DisplayName = "Passa com CamposPublicos nulo — é o pedido legítimo de restaurar o default, NÃO um NotNull")]
    public void Aceita_CamposPublicosNulo()
    {
        ValidationResult result = Validator.Validate(new DefinirConfiguracaoDivulgacaoCommand(
            Guid.CreateVersion7(), null, null, PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Falha quando ProcessoSeletivoId é vazio")]
    public void Rejeita_ProcessoSeletivoIdVazio()
    {
        ValidationResult result = Validator.Validate(new DefinirConfiguracaoDivulgacaoCommand(
            Guid.Empty, null, null, PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ProcessoSeletivoId");
    }
}
