namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Validators;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

public sealed class DefinirOfertaAtendimentoCommandValidatorTests
{
    [Fact(DisplayName = "Validator passa com listas vazias (atendimento não ofertado ainda é válido)")]
    public void Aceita_ListasVazias()
    {
        ValidationResult result = new DefinirOfertaAtendimentoCommandValidator()
            .Validate(new DefinirOfertaAtendimentoCommand(Guid.CreateVersion7(), [], [], []));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Validator falha quando ProcessoSeletivoId é vazio")]
    public void Rejeita_ProcessoSeletivoIdVazio()
    {
        ValidationResult result = new DefinirOfertaAtendimentoCommandValidator()
            .Validate(new DefinirOfertaAtendimentoCommand(Guid.Empty, [], [], []));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ProcessoSeletivoId");
    }

    [Fact(DisplayName = "Validator falha quando um CondicaoId é Guid vazio")]
    public void Rejeita_CondicaoIdVazio()
    {
        ValidationResult result = new DefinirOfertaAtendimentoCommandValidator()
            .Validate(new DefinirOfertaAtendimentoCommand(Guid.CreateVersion7(), [Guid.Empty], [], []));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "CondicaoIds[0]");
    }

    [Fact(DisplayName = "Validator falha quando uma das listas é nula (evita foreach sobre null no handler)")]
    public void Rejeita_ListaNula()
    {
        ValidationResult result = new DefinirOfertaAtendimentoCommandValidator()
            .Validate(new DefinirOfertaAtendimentoCommand(Guid.CreateVersion7(), null!, [], []));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "CondicaoIds");
    }
}
