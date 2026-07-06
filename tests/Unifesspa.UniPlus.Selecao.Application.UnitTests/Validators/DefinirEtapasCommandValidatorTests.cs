namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Validators;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

public sealed class DefinirEtapasCommandValidatorTests
{
    private static EtapaProcessoInput EtapaValida() => new("Prova Objetiva", CaraterEtapa.Classificatoria, 3m, null, 1);

    [Fact(DisplayName = "Validator passa com ao menos uma etapa válida")]
    public void Aceita_ComandoValido()
    {
        ValidationResult result = new DefinirEtapasCommandValidator()
            .Validate(new DefinirEtapasCommand(Guid.CreateVersion7(), [EtapaValida()]));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Validator falha quando a lista de etapas é vazia")]
    public void Rejeita_ListaVazia()
    {
        ValidationResult result = new DefinirEtapasCommandValidator()
            .Validate(new DefinirEtapasCommand(Guid.CreateVersion7(), []));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Etapas");
    }

    [Fact(DisplayName = "Validator falha quando o caráter da etapa é Nenhum")]
    public void Rejeita_CaraterNenhum()
    {
        EtapaProcessoInput etapa = new("Prova Objetiva", CaraterEtapa.Nenhum, 3m, null, 1);

        ValidationResult result = new DefinirEtapasCommandValidator()
            .Validate(new DefinirEtapasCommand(Guid.CreateVersion7(), [etapa]));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Etapas[0].Carater");
    }

    [Fact(DisplayName = "Validator falha quando o peso informado não é positivo")]
    public void Rejeita_PesoNaoPositivo()
    {
        EtapaProcessoInput etapa = new("Prova Objetiva", CaraterEtapa.Classificatoria, 0m, null, 1);

        ValidationResult result = new DefinirEtapasCommandValidator()
            .Validate(new DefinirEtapasCommand(Guid.CreateVersion7(), [etapa]));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Etapas[0].Peso");
    }

    [Fact(DisplayName = "Validator falha quando o peso tem mais de 4 casas decimais (arredondaria para zero)")]
    public void Rejeita_PesoComEscalaExcessiva()
    {
        // 0.00001 > 0 mas numeric(18,4) arredondaria para 0.0000 — divisor da média viraria zero.
        EtapaProcessoInput etapa = new("Prova Objetiva", CaraterEtapa.Classificatoria, 0.00001m, null, 1);

        ValidationResult result = new DefinirEtapasCommandValidator()
            .Validate(new DefinirEtapasCommand(Guid.CreateVersion7(), [etapa]));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Etapas[0].Peso");
    }

    [Fact(DisplayName = "Validator falha quando a nota mínima tem mais de 4 casas decimais")]
    public void Rejeita_NotaMinimaComEscalaExcessiva()
    {
        EtapaProcessoInput etapa = new("Prova Objetiva", CaraterEtapa.Eliminatoria, 3m, 5.00001m, 1);

        ValidationResult result = new DefinirEtapasCommandValidator()
            .Validate(new DefinirEtapasCommand(Guid.CreateVersion7(), [etapa]));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Etapas[0].NotaMinima");
    }

    [Fact(DisplayName = "Validator falha (sem estourar) quando o array de etapas tem item nulo")]
    public void Rejeita_ItemNulo()
    {
        ValidationResult result = new DefinirEtapasCommandValidator()
            .Validate(new DefinirEtapasCommand(Guid.CreateVersion7(), [null!]));

        result.IsValid.Should().BeFalse();
    }
}
