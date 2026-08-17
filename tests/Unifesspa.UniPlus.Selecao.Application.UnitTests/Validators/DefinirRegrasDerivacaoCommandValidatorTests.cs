namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Validators;

using System.Text.Json;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class DefinirRegrasDerivacaoCommandValidatorTests
{
    private static readonly DefinirRegrasDerivacaoCommandValidator Validator = new();

    private static CondicaoDerivacaoInput Condicao() =>
        new("COR_RACA", "IGUAL", JsonSerializer.SerializeToElement("PRETA"));

    private static DefinirRegrasDerivacaoCommand Comando(params RegraDerivacaoInput[] regras) =>
        new(Guid.CreateVersion7(), [new ConfiguracaoDerivacaoInput("MODALIDADE", regras)], PrecondicaoIfMatch.Ausente);

    [Fact(DisplayName = "Passa com lista de configurações vazia — zera as regras")]
    public void Aceita_ListaVazia()
    {
        ValidationResult result = Validator.Validate(new DefinirRegrasDerivacaoCommand(Guid.CreateVersion7(), [], PrecondicaoIfMatch.Ausente));

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
        ValidationResult result = Validator.Validate(new DefinirRegrasDerivacaoCommand(Guid.CreateVersion7(), null!, PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Configuracoes");
    }

    [Fact(DisplayName = "Passa com código do fato vazio no validator — a rejeição é do agregado (ConfiguracaoDerivacaoFato.Criar)")]
    public void Aceita_CodigoFatoVazioNoValidator()
    {
        ValidationResult result = Validator.Validate(new DefinirRegrasDerivacaoCommand(
            Guid.CreateVersion7(), [new ConfiguracaoDerivacaoInput("", [new RegraDerivacaoInput(0, "AC", null)])], PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Passa sem regra alguma no validator — a rejeição é do agregado (ConfiguracaoDerivacaoFato.Criar)")]
    public void Aceita_SemRegrasNoValidator()
    {
        ValidationResult result = Validator.Validate(new DefinirRegrasDerivacaoCommand(
            Guid.CreateVersion7(), [new ConfiguracaoDerivacaoInput("MODALIDADE", [])], PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Passa com ordem negativa no validator — a rejeição é do agregado (RegraDerivacaoConfigurada.Criar)")]
    public void Aceita_OrdemNegativaNoValidator()
    {
        ValidationResult result = Validator.Validate(Comando(new RegraDerivacaoInput(-1, "AC", null)));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Passa com contribui vazio no validator — a rejeição é do agregado (RegraDerivacaoConfigurada.Criar)")]
    public void Aceita_ContribuiVazioNoValidator()
    {
        ValidationResult result = Validator.Validate(Comando(new RegraDerivacaoInput(0, "", null)));

        result.IsValid.Should().BeTrue();
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
