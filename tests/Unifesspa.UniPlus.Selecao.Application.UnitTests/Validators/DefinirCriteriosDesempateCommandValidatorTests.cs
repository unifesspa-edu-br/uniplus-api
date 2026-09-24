namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Validators;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class DefinirCriteriosDesempateCommandValidatorTests
{
    [Fact(DisplayName = "Validator passa com lista de critérios vazia (dimensão opcional)")]
    public void Aceita_ListaVazia()
    {
        ValidationResult result = new DefinirCriteriosDesempateCommandValidator()
            .Validate(new DefinirCriteriosDesempateCommand(Guid.CreateVersion7(), [], PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Validator falha quando Criterios é nulo (payload malformado)")]
    public void Rejeita_CriteriosNulo()
    {
        ValidationResult result = new DefinirCriteriosDesempateCommandValidator()
            .Validate(new DefinirCriteriosDesempateCommand(Guid.CreateVersion7(), null!, PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Criterios");
    }

    [Fact(DisplayName = "Validator passa com Ordem não positiva — a rejeição é do agregado (CriterioDesempate.Criar)")]
    public void Aceita_OrdemInvalidaNoValidator()
    {
        ValidationResult result = new DefinirCriteriosDesempateCommandValidator().Validate(
            new DefinirCriteriosDesempateCommand(
                Guid.CreateVersion7(),
                [new CriterioDesempateInput(0, "DESEMPATE-MAIOR-IDADE", "v1", null, null, null, null, null)], PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Acima do teto o validator não confere os itens nem recusa: a recusa é do agregado")]
    public void AcimaDoTeto_NaoConfereItens()
    {
        CriterioDesempateInput[] criterios = [.. Enumerable.Range(1, 10_000).Select(static ordem =>
            new CriterioDesempateInput(ordem, string.Empty, string.Empty, null, null, null, null, null))];

        ValidationResult result = new DefinirCriteriosDesempateCommandValidator().Validate(
            new DefinirCriteriosDesempateCommand(Guid.CreateVersion7(), criterios, PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "No teto, as regras por item continuam valendo")]
    public void NoTeto_RegrasPorItemValem()
    {
        CriterioDesempateInput[] criterios = [.. Enumerable.Range(1, ProcessoSeletivo.CriteriosDesempateMaximo).Select(static ordem =>
            new CriterioDesempateInput(ordem, string.Empty, "v1", null, null, null, null, null))];

        ValidationResult result = new DefinirCriteriosDesempateCommandValidator().Validate(
            new DefinirCriteriosDesempateCommand(Guid.CreateVersion7(), criterios, PrecondicaoIfMatch.Ausente));

        result.Errors.Should().HaveCount(ProcessoSeletivo.CriteriosDesempateMaximo)
            .And.OnlyContain(static e => e.PropertyName.EndsWith(".RegraCodigo", StringComparison.Ordinal));
    }
}
