namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Validators;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class DefinirDistribuicaoVagasCommandValidatorTests
{
    private readonly DefinirDistribuicaoVagasCommandValidator _validator = new();

    private static ConfiguracaoDistribuicaoVagasInput ItemValido() => new(
        Guid.CreateVersion7(), 50, 0.5m, "DISTRIB-VAGAS-LEI-12711", "v1", null, null, null, [Guid.CreateVersion7()], []);

    [Fact(DisplayName = "Command válido não gera erros")]
    public void Validar_Valido_SemErros()
    {
        DefinirDistribuicaoVagasCommand command = new(Guid.CreateVersion7(), [ItemValido()], PrecondicaoIfMatch.Ausente);

        ValidationResult resultado = _validator.Validate(command);

        resultado.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Lista de distribuição vazia gera erro")]
    public void Validar_ListaVazia_GeraErro()
    {
        DefinirDistribuicaoVagasCommand command = new(Guid.CreateVersion7(), [], PrecondicaoIfMatch.Ausente);

        ValidationResult resultado = _validator.Validate(command);

        resultado.IsValid.Should().BeFalse();
    }

    [Theory(DisplayName = "Validator passa com VO_base não positivo — a rejeição é do agregado (ConfiguracaoDistribuicaoVagas.Criar, ADR-0125)")]
    [InlineData(0)]
    [InlineData(-1)]
    public void Aceita_VoBaseInvalidoNoValidator(int voBase)
    {
        ConfiguracaoDistribuicaoVagasInput item = ItemValido() with { VoBase = voBase };
        DefinirDistribuicaoVagasCommand command = new(Guid.CreateVersion7(), [item], PrecondicaoIfMatch.Ausente);

        ValidationResult resultado = _validator.Validate(command);

        resultado.IsValid.Should().BeTrue();
    }

    [Theory(DisplayName = "Validator passa com PR fora de [0,5; 1] — a rejeição é do agregado (ConfiguracaoDistribuicaoVagas.Criar, ADR-0125)")]
    [InlineData(0.49)]
    [InlineData(1.01)]
    public void Aceita_PrForaDoLimiteNoValidator(double pr)
    {
        ConfiguracaoDistribuicaoVagasInput item = ItemValido() with { Pr = (decimal)pr };
        DefinirDistribuicaoVagasCommand command = new(Guid.CreateVersion7(), [item], PrecondicaoIfMatch.Ausente);

        ValidationResult resultado = _validator.Validate(command);

        resultado.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Validator falha quando o PR tem mais de 4 casas decimais (arredondaria silenciosamente)")]
    public void Rejeita_PrComEscalaExcessiva()
    {
        ConfiguracaoDistribuicaoVagasInput item = ItemValido() with { Pr = 0.75001m };
        DefinirDistribuicaoVagasCommand command = new(Guid.CreateVersion7(), [item], PrecondicaoIfMatch.Ausente);

        ValidationResult resultado = _validator.Validate(command);

        resultado.IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "Validator passa com lista de modalidades vazia — a rejeição é do agregado (ConfiguracaoDistribuicaoVagas.Criar, ADR-0125)")]
    public void Aceita_ModalidadeIdsVazioNoValidator()
    {
        ConfiguracaoDistribuicaoVagasInput item = ItemValido() with { ModalidadeIds = [] };
        DefinirDistribuicaoVagasCommand command = new(Guid.CreateVersion7(), [item], PrecondicaoIfMatch.Ausente);

        ValidationResult resultado = _validator.Validate(command);

        resultado.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Validator falha (sem estourar) quando ModalidadeIds é nulo")]
    public void Rejeita_ModalidadeIdsNulo()
    {
        ConfiguracaoDistribuicaoVagasInput item = ItemValido() with { ModalidadeIds = null! };
        DefinirDistribuicaoVagasCommand command = new(Guid.CreateVersion7(), [item], PrecondicaoIfMatch.Ausente);

        ValidationResult resultado = _validator.Validate(command);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == "DistribuicaoVagas[0].ModalidadeIds");
    }

    [Fact(DisplayName = "Item nulo na lista gera erro, sem lançar exceção")]
    public void Validar_ItemNulo_GeraErro()
    {
        DefinirDistribuicaoVagasCommand command = new(Guid.CreateVersion7(), [null!], PrecondicaoIfMatch.Ausente);

        ValidationResult resultado = _validator.Validate(command);

        resultado.IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "Quadro nulo gera erro de validação, sem lançar exceção")]
    public void Validar_QuadroNulo_GeraErroSemLancarExcecao()
    {
        ConfiguracaoDistribuicaoVagasInput item = ItemValido() with { Quadro = null! };
        DefinirDistribuicaoVagasCommand command = new(Guid.CreateVersion7(), [item], PrecondicaoIfMatch.Ausente);

        ValidationResult resultado = _validator.Validate(command);

        resultado.IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "Item nulo dentro do quadro gera erro de validação, sem lançar exceção")]
    public void Validar_ItemNuloDentroDoQuadro_GeraErroSemLancarExcecao()
    {
        ConfiguracaoDistribuicaoVagasInput item = ItemValido() with { Quadro = [null!] };
        DefinirDistribuicaoVagasCommand command = new(Guid.CreateVersion7(), [item], PrecondicaoIfMatch.Ausente);

        ValidationResult resultado = _validator.Validate(command);

        resultado.IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "Quantidade negativa no quadro gera erro")]
    public void Validar_QuadroComQuantidadeNegativa_GeraErro()
    {
        ConfiguracaoDistribuicaoVagasInput item = ItemValido() with
        {
            Quadro = [new QuantidadeVagaInput(Guid.CreateVersion7(), -1)],
        };
        DefinirDistribuicaoVagasCommand command = new(Guid.CreateVersion7(), [item], PrecondicaoIfMatch.Ausente);

        ValidationResult resultado = _validator.Validate(command);

        resultado.IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "ModalidadeId vazio no quadro gera erro")]
    public void Validar_QuadroComModalidadeIdVazio_GeraErro()
    {
        ConfiguracaoDistribuicaoVagasInput item = ItemValido() with
        {
            Quadro = [new QuantidadeVagaInput(Guid.Empty, 10)],
        };
        DefinirDistribuicaoVagasCommand command = new(Guid.CreateVersion7(), [item], PrecondicaoIfMatch.Ausente);

        ValidationResult resultado = _validator.Validate(command);

        resultado.IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "ModalidadeId repetido no quadro gera erro")]
    public void Validar_QuadroComModalidadeIdRepetido_GeraErro()
    {
        Guid modalidadeId = Guid.CreateVersion7();
        ConfiguracaoDistribuicaoVagasInput item = ItemValido() with
        {
            Quadro = [new QuantidadeVagaInput(modalidadeId, 10), new QuantidadeVagaInput(modalidadeId, 20)],
        };
        DefinirDistribuicaoVagasCommand command = new(Guid.CreateVersion7(), [item], PrecondicaoIfMatch.Ausente);

        ValidationResult resultado = _validator.Validate(command);

        resultado.IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "Quadro com quantidade válida não gera erro")]
    public void Validar_QuadroValido_SemErros()
    {
        ConfiguracaoDistribuicaoVagasInput item = ItemValido() with
        {
            Quadro = [new QuantidadeVagaInput(Guid.CreateVersion7(), 10)],
        };
        DefinirDistribuicaoVagasCommand command = new(Guid.CreateVersion7(), [item], PrecondicaoIfMatch.Ausente);

        ValidationResult resultado = _validator.Validate(command);

        resultado.IsValid.Should().BeTrue();
    }
}
