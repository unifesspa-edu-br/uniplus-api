namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Validators;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

public sealed class DefinirDistribuicaoVagasCommandValidatorTests
{
    private readonly DefinirDistribuicaoVagasCommandValidator _validator = new();

    private static ConfiguracaoDistribuicaoVagasInput ItemValido() => new(
        Guid.CreateVersion7(), 50, 0.5m, "DISTRIB-VAGAS-LEI-12711", "v1", null, [Guid.CreateVersion7()]);

    [Fact(DisplayName = "Command válido não gera erros")]
    public void Validar_Valido_SemErros()
    {
        DefinirDistribuicaoVagasCommand command = new(Guid.CreateVersion7(), [ItemValido()]);

        ValidationResult resultado = _validator.Validate(command);

        resultado.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Lista de distribuição vazia gera erro")]
    public void Validar_ListaVazia_GeraErro()
    {
        DefinirDistribuicaoVagasCommand command = new(Guid.CreateVersion7(), []);

        ValidationResult resultado = _validator.Validate(command);

        resultado.IsValid.Should().BeFalse();
    }

    [Theory(DisplayName = "VO_base não positivo gera erro")]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validar_VoBaseInvalido_GeraErro(int voBase)
    {
        ConfiguracaoDistribuicaoVagasInput item = ItemValido() with { VoBase = voBase };
        DefinirDistribuicaoVagasCommand command = new(Guid.CreateVersion7(), [item]);

        ValidationResult resultado = _validator.Validate(command);

        resultado.IsValid.Should().BeFalse();
    }

    [Theory(DisplayName = "PR fora de [0,5; 1] gera erro")]
    [InlineData(0.49)]
    [InlineData(1.01)]
    public void Validar_PrForaDoLimite_GeraErro(double pr)
    {
        ConfiguracaoDistribuicaoVagasInput item = ItemValido() with { Pr = (decimal)pr };
        DefinirDistribuicaoVagasCommand command = new(Guid.CreateVersion7(), [item]);

        ValidationResult resultado = _validator.Validate(command);

        resultado.IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "Lista de modalidades vazia gera erro")]
    public void Validar_ModalidadeIdsVazio_GeraErro()
    {
        ConfiguracaoDistribuicaoVagasInput item = ItemValido() with { ModalidadeIds = [] };
        DefinirDistribuicaoVagasCommand command = new(Guid.CreateVersion7(), [item]);

        ValidationResult resultado = _validator.Validate(command);

        resultado.IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "Item nulo na lista gera erro, sem lançar exceção")]
    public void Validar_ItemNulo_GeraErro()
    {
        DefinirDistribuicaoVagasCommand command = new(Guid.CreateVersion7(), [null!]);

        ValidationResult resultado = _validator.Validate(command);

        resultado.IsValid.Should().BeFalse();
    }
}
