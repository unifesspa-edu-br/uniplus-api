namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Validators;

using System.Text.Json;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class DefinirFatosColetadosCommandValidatorTests
{
    private static readonly DefinirFatosColetadosCommandValidator Validator = new();

    private static CondicaoPrecondicaoInput Condicao(string fato) =>
        new(fato, "IGUAL", JsonSerializer.SerializeToElement("PRETA"));

    [Fact(DisplayName = "Passa com lista de fatos vazia — zera a coleta")]
    public void Aceita_ListaVazia()
    {
        ValidationResult result = Validator.Validate(new DefinirFatosColetadosCommand(Guid.CreateVersion7(), [], PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Passa com fato sem pré-condição (null)")]
    public void Aceita_FatoSemPrecondicao()
    {
        ValidationResult result = Validator.Validate(new DefinirFatosColetadosCommand(
            Guid.CreateVersion7(), [new FatoColetadoInput("COR_RACA", 0, "Cor ou raça", "SELECAO_UNICA", false, null)], PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Falha quando a lista de fatos é nula (payload malformado)")]
    public void Rejeita_FatosNulo()
    {
        ValidationResult result = Validator.Validate(new DefinirFatosColetadosCommand(Guid.CreateVersion7(), null!, PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Fatos");
    }

    [Fact(DisplayName = "Falha quando o código do fato é vazio")]
    public void Rejeita_FatoCodigoVazio()
    {
        ValidationResult result = Validator.Validate(new DefinirFatosColetadosCommand(
            Guid.CreateVersion7(), [new FatoColetadoInput("", 0, "Rótulo", "SELECAO_UNICA", false, null)], PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Fatos[0].FatoCodigo");
    }

    [Fact(DisplayName = "Falha quando o rótulo é vazio")]
    public void Rejeita_RotuloVazio()
    {
        ValidationResult result = Validator.Validate(new DefinirFatosColetadosCommand(
            Guid.CreateVersion7(), [new FatoColetadoInput("COR_RACA", 0, "", "SELECAO_UNICA", false, null)], PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Fatos[0].Rotulo");
    }

    [Fact(DisplayName = "Falha quando o rótulo excede 300 caracteres — mesmo limite da coluna, recusado antes do SaveChanges")]
    public void Rejeita_RotuloExcedeLimite()
    {
        ValidationResult result = Validator.Validate(new DefinirFatosColetadosCommand(
            Guid.CreateVersion7(), [new FatoColetadoInput("COR_RACA", 0, new string('a', 301), "SELECAO_UNICA", false, null)], PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Fatos[0].Rotulo");
    }

    [Fact(DisplayName = "Falha quando a ordem é negativa")]
    public void Rejeita_OrdemNegativa()
    {
        ValidationResult result = Validator.Validate(new DefinirFatosColetadosCommand(
            Guid.CreateVersion7(), [new FatoColetadoInput("COR_RACA", -1, "Cor ou raça", "SELECAO_UNICA", false, null)], PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Fatos[0].Ordem");
    }

    [Fact(DisplayName = "Falha quando a pré-condição presente é uma lista externa vazia (ausência é null)")]
    public void Rejeita_PrecondicaoListaExternaVazia()
    {
        ValidationResult result = Validator.Validate(new DefinirFatosColetadosCommand(
            Guid.CreateVersion7(), [new FatoColetadoInput("BAIXA_RENDA", 0, "Baixa renda", "BOOLEANO", false, [])], PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Fatos[0].Precondicao");
    }

    [Fact(DisplayName = "Falha quando a pré-condição tem uma cláusula interna vazia")]
    public void Rejeita_PrecondicaoClausulaVazia()
    {
        ValidationResult result = Validator.Validate(new DefinirFatosColetadosCommand(
            Guid.CreateVersion7(), [new FatoColetadoInput("BAIXA_RENDA", 0, "Baixa renda", "BOOLEANO", false, [[]])], PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Fatos[0].Precondicao");
    }

    [Fact(DisplayName = "Falha quando a pré-condição tem uma condição nula ([[null]])")]
    public void Rejeita_PrecondicaoCondicaoNula()
    {
        ValidationResult result = Validator.Validate(new DefinirFatosColetadosCommand(
            Guid.CreateVersion7(), [new FatoColetadoInput("BAIXA_RENDA", 0, "Baixa renda", "BOOLEANO", false, [[null!]])], PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Fatos[0].Precondicao");
    }

    [Fact(DisplayName = "Passa com pré-condição bem-formada (uma cláusula, uma condição)")]
    public void Aceita_PrecondicaoBemFormada()
    {
        ValidationResult result = Validator.Validate(new DefinirFatosColetadosCommand(
            Guid.CreateVersion7(), [new FatoColetadoInput("BAIXA_RENDA", 0, "Baixa renda", "BOOLEANO", false, [[Condicao("COR_RACA")]])], PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeTrue();
    }
}
