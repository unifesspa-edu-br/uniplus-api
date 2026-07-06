namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Validators;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

public sealed class CriarProcessoSeletivoCommandValidatorTests
{
    [Fact(DisplayName = "Validator passa com nome e tipo válidos")]
    public void Aceita_ComandoValido()
    {
        ValidationResult result = new CriarProcessoSeletivoCommandValidator()
            .Validate(new CriarProcessoSeletivoCommand("PS 2026 — SiSU", TipoProcesso.SiSU));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Validator falha quando nome é vazio")]
    public void Rejeita_NomeVazio()
    {
        ValidationResult result = new CriarProcessoSeletivoCommandValidator()
            .Validate(new CriarProcessoSeletivoCommand(string.Empty, TipoProcesso.SiSU));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "Nome");
    }

    [Fact(DisplayName = "Validator falha quando tipo é Nenhum")]
    public void Rejeita_TipoNenhum()
    {
        ValidationResult result = new CriarProcessoSeletivoCommandValidator()
            .Validate(new CriarProcessoSeletivoCommand("PS 2026", TipoProcesso.Nenhum));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Tipo");
    }
}
