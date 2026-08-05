namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Validators;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

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

    [Fact(DisplayName = "Falha com lista vazia — única recusa incondicional")]
    public void Rejeita_ListaVazia()
    {
        ValidationResult result = Validator.Validate(new DefinirConfiguracaoDivulgacaoCommand(
            Guid.CreateVersion7(), [], null, PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "Falha com campo fora do vocabulário")]
    public void Rejeita_CampoForaDoVocabulario()
    {
        ValidationResult result = Validator.Validate(new DefinirConfiguracaoDivulgacaoCommand(
            Guid.CreateVersion7(), ["numero_inscricao", "cpf"], null, PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "Falha sem numero_inscricao")]
    public void Rejeita_SemNumeroInscricao()
    {
        ValidationResult result = Validator.Validate(new DefinirConfiguracaoDivulgacaoCommand(
            Guid.CreateVersion7(), [ConfiguracaoDivulgacao.NomeAbreviado], null, PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "Falha com nome_abreviado e nome juntos")]
    public void Rejeita_NomeENomeAbreviadoJuntos()
    {
        ValidationResult result = Validator.Validate(new DefinirConfiguracaoDivulgacaoCommand(
            Guid.CreateVersion7(),
            [ConfiguracaoDivulgacao.NumeroInscricao, ConfiguracaoDivulgacao.NomeAbreviado, ConfiguracaoDivulgacao.Nome],
            "justificativa", PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "Falha com nome sem justificativa")]
    public void Rejeita_NomeSemJustificativa()
    {
        ValidationResult result = Validator.Validate(new DefinirConfiguracaoDivulgacaoCommand(
            Guid.CreateVersion7(), [ConfiguracaoDivulgacao.NumeroInscricao, ConfiguracaoDivulgacao.Nome], null, PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "Passa com nome_abreviado sem justificativa — só nome exige")]
    public void Aceita_NomeAbreviadoSemJustificativa()
    {
        ValidationResult result = Validator.Validate(new DefinirConfiguracaoDivulgacaoCommand(
            Guid.CreateVersion7(), [ConfiguracaoDivulgacao.NumeroInscricao, ConfiguracaoDivulgacao.NomeAbreviado], null, PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Falha quando a justificativa excede o limite da coluna")]
    public void Rejeita_JustificativaExcedeLimite()
    {
        ValidationResult result = Validator.Validate(new DefinirConfiguracaoDivulgacaoCommand(
            Guid.CreateVersion7(),
            [ConfiguracaoDivulgacao.NumeroInscricao, ConfiguracaoDivulgacao.Nome],
            new string('a', ConfiguracaoDivulgacao.JustificativaMaxLength + 1),
            PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Justificativa");
    }

    [Fact(DisplayName = "Passa quando o valor bruto excede o limite, mas a forma Trim+NFC tem exatamente o limite")]
    public void Aceita_JustificativaBrutaAcimaDoLimiteMasNormalizadaNoLimite()
    {
        // 1000 letras 'a' entre espaços: bruto tem 1002 caracteres, mas a forma que
        // ConfiguracaoDivulgacao.Criar mede (Trim+NFC) tem exatamente o limite — o validator
        // não pode ser mais restritivo que o domínio, que aceitaria este valor.
        string justificativaComEspacosNasBordas = $" {new string('a', ConfiguracaoDivulgacao.JustificativaMaxLength)} ";

        ValidationResult result = Validator.Validate(new DefinirConfiguracaoDivulgacaoCommand(
            Guid.CreateVersion7(),
            [ConfiguracaoDivulgacao.NumeroInscricao, ConfiguracaoDivulgacao.Nome],
            justificativaComEspacosNasBordas,
            PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Falha quando a justificativa contém o caractere nulo (U+0000)")]
    public void Rejeita_JustificativaComCaractereNulo()
    {
        // Por código de caractere, não como literal '\0' no fonte — mesmo raciocínio de
        // ConfiguracaoDivulgacao.CaractereNulo: evita gravar o byte cru no arquivo de teste.
        string justificativaComCaractereNulo = $"Justificativa com {(char)0} no meio.";

        ValidationResult result = Validator.Validate(new DefinirConfiguracaoDivulgacaoCommand(
            Guid.CreateVersion7(),
            [ConfiguracaoDivulgacao.NumeroInscricao, ConfiguracaoDivulgacao.Nome],
            justificativaComCaractereNulo,
            PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Justificativa");
    }
}
