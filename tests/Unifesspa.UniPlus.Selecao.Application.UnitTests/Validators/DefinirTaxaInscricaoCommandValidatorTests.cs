namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Validators;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class DefinirTaxaInscricaoCommandValidatorTests
{
    private static readonly DefinirTaxaInscricaoCommandValidator Validator = new();

    [Fact(DisplayName = "Passa com Cobra nulo — pedido de remover a declaração")]
    public void Aceita_CobraNulo()
    {
        ValidationResult result = Validator.Validate(new DefinirTaxaInscricaoCommand(
            Guid.CreateVersion7(), null, null, null, false, PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Falha com ProcessoSeletivoId vazio")]
    public void Rejeita_ProcessoSeletivoIdVazio()
    {
        ValidationResult result = Validator.Validate(new DefinirTaxaInscricaoCommand(
            Guid.Empty, true, 100m, null, false, PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeFalse();
    }

    [Theory(DisplayName = "Passa com Valor não positivo — o validator não decide isso, é regra de domínio (ValorObrigatorioQuandoCobra)")]
    [InlineData(0)]
    [InlineData(-1)]
    public void Aceita_ValorNaoPositivo(double valor)
    {
        ValidationResult result = Validator.Validate(new DefinirTaxaInscricaoCommand(
            Guid.CreateVersion7(), true, (decimal)valor, null, false, PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Falha com Valor de mais de duas casas decimais")]
    public void Rejeita_ValorComEscalaExcedida()
    {
        ValidationResult result = Validator.Validate(new DefinirTaxaInscricaoCommand(
            Guid.CreateVersion7(), true, 100.001m, null, false, PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "Passa com Valor nulo — o validator não decide se Cobra exige Valor, isso é regra de domínio")]
    public void Aceita_ValorNulo()
    {
        ValidationResult result = Validator.Validate(new DefinirTaxaInscricaoCommand(
            Guid.CreateVersion7(), true, null, null, false, PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Passa com Fundamentos=[] — lista vazia é zero fundamentos, estado válido (CA-04, achado de revisão P1)")]
    public void Aceita_FundamentosListaVazia()
    {
        ValidationResult result = Validator.Validate(new DefinirTaxaInscricaoCommand(
            Guid.CreateVersion7(), true, 100m, [], false, PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Passa com Fundamentos nulo")]
    public void Aceita_FundamentosNulo()
    {
        ValidationResult result = Validator.Validate(new DefinirTaxaInscricaoCommand(
            Guid.CreateVersion7(), true, 100m, null, false, PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Passa com comando completo e válido")]
    public void Aceita_ComandoCompleto()
    {
        ValidationResult result = Validator.Validate(new DefinirTaxaInscricaoCommand(
            Guid.CreateVersion7(), true, 150.00m, [FundamentoIsencaoCodigo.CadastroUnico], true, PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeTrue();
    }
}
