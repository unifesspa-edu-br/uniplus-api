namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Validators;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class DefinirCriteriosDesempateCommandValidatorTests
{
    [Fact(DisplayName = "Validator passa com lista de critérios vazia (a exigência é da publicação)")]
    public void Aceita_ListaVazia()
    {
        ValidationResult result = new DefinirCriteriosDesempateCommandValidator()
            .Validate(new DefinirCriteriosDesempateCommand(Guid.CreateVersion7(), []));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Validator falha quando Criterios é nulo (payload malformado)")]
    public void Rejeita_CriteriosNulo()
    {
        ValidationResult result = new DefinirCriteriosDesempateCommandValidator()
            .Validate(new DefinirCriteriosDesempateCommand(Guid.CreateVersion7(), null!));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Criterios");
    }

    [Fact(DisplayName = "Validator passa com Ordem não positiva — a rejeição é do agregado (CriterioDesempate.Criar)")]
    public void Aceita_OrdemInvalidaNoValidator()
    {
        ValidationResult result = new DefinirCriteriosDesempateCommandValidator().Validate(
            new DefinirCriteriosDesempateCommand(
                Guid.CreateVersion7(),
                [CriterioDesempateDeTeste.DaRegra("DESEMPATE-MAIOR-IDADE").NaOrdem(0).Construir()]));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Acima do teto o validator não confere os itens nem recusa: a recusa é do agregado")]
    public void AcimaDoTeto_NaoConfereItens()
    {
        CriterioDesempateInput[] criterios = [.. Enumerable.Range(1, 10_000).Select(static ordem =>
            CriterioDesempateDeTeste.DaRegra(string.Empty).NaVersao(string.Empty).NaOrdem(ordem).Construir())];

        ValidationResult result = new DefinirCriteriosDesempateCommandValidator().Validate(
            new DefinirCriteriosDesempateCommand(Guid.CreateVersion7(), criterios));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "No teto, as regras por item continuam valendo")]
    public void NoTeto_RegrasPorItemValem()
    {
        CriterioDesempateInput[] criterios = [.. Enumerable.Range(1, RegrasDoDesempate.CriteriosDesempateMaximo).Select(static ordem =>
            CriterioDesempateDeTeste.DaRegra(string.Empty).NaOrdem(ordem).Construir())];

        ValidationResult result = new DefinirCriteriosDesempateCommandValidator().Validate(
            new DefinirCriteriosDesempateCommand(Guid.CreateVersion7(), criterios));

        result.Errors.Should().HaveCount(RegrasDoDesempate.CriteriosDesempateMaximo)
            .And.OnlyContain(static e => e.PropertyName.EndsWith(".Regra.Codigo", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Critério sem a referência da regra é recusado com 400 em Regra, sem desreferenciar o ausente")]
    public void CriterioSemRegra_RecusaARegra()
    {
        ValidationResult result = new DefinirCriteriosDesempateCommandValidator().Validate(
            new DefinirCriteriosDesempateCommand(
                Guid.CreateVersion7(), [CriterioDesempateDeTeste.DaRegra("DESEMPATE-MAIOR-IDADE").SemRegra().Construir()]));

        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("Criterios[0].Regra");
    }

    [Fact(DisplayName = "Regra sem código e sem versão é recusada com 400 em cada campo da referência")]
    public void RegraSemCodigoESemVersao_RecusaOsDoisCampos()
    {
        ValidationResult result = new DefinirCriteriosDesempateCommandValidator().Validate(
            new DefinirCriteriosDesempateCommand(
                Guid.CreateVersion7(), [CriterioDesempateDeTeste.DaRegra(string.Empty).NaVersao(null).Construir()]));

        result.Errors.Select(static e => e.PropertyName).Should().BeEquivalentTo(
            ["Criterios[0].Regra.Codigo", "Criterios[0].Regra.Versao"]);
    }
}
