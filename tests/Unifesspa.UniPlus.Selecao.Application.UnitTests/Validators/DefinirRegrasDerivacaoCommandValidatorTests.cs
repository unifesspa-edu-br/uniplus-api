namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Validators;

using System.Text.Json;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

public sealed class DefinirRegrasDerivacaoCommandValidatorTests
{
    private static readonly DefinirRegrasDerivacaoCommandValidator Validator = new();

    private static CondicaoDerivacaoInput Condicao() =>
        new("COR_RACA", "IGUAL", JsonSerializer.SerializeToElement("PRETA"));

    private static DefinirRegrasDerivacaoCommand Comando(params RegraDerivacaoInput[] regras) =>
        new(Guid.CreateVersion7(), [new ConfiguracaoDerivacaoInput("MODALIDADE", regras)]);

    [Fact(DisplayName = "Passa com lista de configurações vazia — zera as regras")]
    public void Aceita_ListaVazia()
    {
        ValidationResult result = Validator.Validate(new DefinirRegrasDerivacaoCommand(Guid.CreateVersion7(), []));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Passa com regra âncora (Quando null)")]
    public void Aceita_RegraAncora()
    {
        ValidationResult result = Validator.Validate(Comando(new RegraDerivacaoInput(0, "AC", null)));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Passa com regra condicional bem-formada")]
    public void Aceita_RegraCondicional()
    {
        ValidationResult result = Validator.Validate(Comando(new RegraDerivacaoInput(0, "AC", [[Condicao()]])));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Falha quando a lista de configurações é nula")]
    public void Rejeita_ConfiguracoesNulo()
    {
        ValidationResult result = Validator.Validate(new DefinirRegrasDerivacaoCommand(Guid.CreateVersion7(), null!));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Configuracoes");
    }

    [Fact(DisplayName = "Falha quando o código do fato é vazio")]
    public void Rejeita_CodigoFatoVazio()
    {
        ValidationResult result = Validator.Validate(new DefinirRegrasDerivacaoCommand(
            Guid.CreateVersion7(), [new ConfiguracaoDerivacaoInput("", [new RegraDerivacaoInput(0, "AC", null)])]));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Configuracoes[0].CodigoFato");
    }

    [Fact(DisplayName = "Falha quando a configuração não tem regra alguma")]
    public void Rejeita_SemRegras()
    {
        ValidationResult result = Validator.Validate(new DefinirRegrasDerivacaoCommand(
            Guid.CreateVersion7(), [new ConfiguracaoDerivacaoInput("MODALIDADE", [])]));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Configuracoes[0].Regras");
    }

    [Fact(DisplayName = "Falha quando a ordem da regra é negativa")]
    public void Rejeita_OrdemNegativa()
    {
        ValidationResult result = Validator.Validate(Comando(new RegraDerivacaoInput(-1, "AC", null)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Configuracoes[0].Regras[0].Ordem");
    }

    [Fact(DisplayName = "Falha quando contribui é vazio")]
    public void Rejeita_ContribuiVazio()
    {
        ValidationResult result = Validator.Validate(Comando(new RegraDerivacaoInput(0, "", null)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Configuracoes[0].Regras[0].Contribui");
    }

    [Fact(DisplayName = "Falha quando o 'quando' presente é uma lista externa vazia (âncora é null)")]
    public void Rejeita_QuandoListaExternaVazia()
    {
        ValidationResult result = Validator.Validate(Comando(new RegraDerivacaoInput(0, "AC", [])));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Configuracoes[0].Regras[0].Quando");
    }

    [Fact(DisplayName = "Falha quando o 'quando' tem uma condição nula ([[null]])")]
    public void Rejeita_QuandoCondicaoNula()
    {
        ValidationResult result = Validator.Validate(Comando(new RegraDerivacaoInput(0, "AC", [[null!]])));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Configuracoes[0].Regras[0].Quando");
    }
}
