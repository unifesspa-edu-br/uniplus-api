namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Validators;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

public sealed class DefinirClassificacaoCommandValidatorTests
{
    private static DefinirClassificacaoCommand ComandoValido(IReadOnlyList<RegraEliminacaoInput> regrasEliminacao) => new(
        Guid.CreateVersion7(), "FORMULA-MEDIA-PONDERADA", "v1", "PRECISAO-TRUNCAR", "v1", 2,
        "ALOCACAO-OPCOES-RN04", "v1", 1, regrasEliminacao);

    [Fact(DisplayName = "Validator passa com comando válido e lista de eliminação vazia")]
    public void Aceita_ComandoValido()
    {
        ValidationResult result = new DefinirClassificacaoCommandValidator().Validate(ComandoValido([]));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Validator falha quando RegrasEliminacao é nulo (payload malformado)")]
    public void Rejeita_RegrasEliminacaoNula()
    {
        ValidationResult result = new DefinirClassificacaoCommandValidator().Validate(ComandoValido(null!));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "RegrasEliminacao");
    }

    [Fact(DisplayName = "Validator falha quando NOpcoesAlocacao está fora de {1,2}")]
    public void Rejeita_NOpcoesForaDeIntervalo()
    {
        DefinirClassificacaoCommand comando = ComandoValido([]) with { NOpcoesAlocacao = 3 };

        ValidationResult result = new DefinirClassificacaoCommandValidator().Validate(comando);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "NOpcoesAlocacao");
    }

    [Fact(DisplayName = "Validator falha quando código de arredondamento é informado sem versão")]
    public void Rejeita_ArredondamentoSemVersao()
    {
        DefinirClassificacaoCommand comando = ComandoValido([]) with { RegraArredondamentoVersao = null };

        ValidationResult result = new DefinirClassificacaoCommandValidator().Validate(comando);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "RegraArredondamentoVersao");
    }
}
