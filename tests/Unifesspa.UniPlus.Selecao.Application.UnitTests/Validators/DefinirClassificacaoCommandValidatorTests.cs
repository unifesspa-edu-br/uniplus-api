namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Validators;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class DefinirClassificacaoCommandValidatorTests
{
    private static DefinirClassificacaoCommand ComandoValido(IReadOnlyList<RegraEliminacaoInput> regrasEliminacao) => new(
        Guid.CreateVersion7(), "FORMULA_MEDIA_PONDERADA", "v1", "PRECISAO_TRUNCAR", "v1", 2,
        "ALOCACAO_PRIMEIRA_OPCAO_PRIORITARIA", "v1", regrasEliminacao, false, null, null);

    [Fact(DisplayName = "Validator passa com comando válido e lista de eliminação vazia")]
    public void Aceita_ComandoValido()
    {
        ValidationResult result = new DefinirClassificacaoCommandValidator().Validate(ComandoValido([]));

        result.IsValid.Should().BeTrue();
    }

    [Theory(DisplayName = "Validator recusa cada código e versão de regra obrigatórios ausente, nulo, vazio ou em branco, no campo")]
    [InlineData("RegraCalculoCodigo", null)]
    [InlineData("RegraCalculoCodigo", "")]
    [InlineData("RegraCalculoVersao", null)]
    [InlineData("RegraCalculoVersao", "  ")]
    [InlineData("RegraOrdemAlocacaoCodigo", null)]
    [InlineData("RegraOrdemAlocacaoCodigo", "")]
    [InlineData("RegraOrdemAlocacaoVersao", null)]
    [InlineData("RegraOrdemAlocacaoVersao", "  ")]
    public void Rejeita_RegraObrigatoriaAusente(string campo, string? valor)
    {
        DefinirClassificacaoCommand comando = ComandoValido([]) with
        {
            RegraCalculoCodigo = campo == "RegraCalculoCodigo" ? valor : "FORMULA_MEDIA_PONDERADA",
            RegraCalculoVersao = campo == "RegraCalculoVersao" ? valor : "v1",
            RegraOrdemAlocacaoCodigo = campo == "RegraOrdemAlocacaoCodigo" ? valor : "ALOCACAO_PRIMEIRA_OPCAO_PRIORITARIA",
            RegraOrdemAlocacaoVersao = campo == "RegraOrdemAlocacaoVersao" ? valor : "v1",
        };

        ValidationResult result = new DefinirClassificacaoCommandValidator().Validate(comando);

        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be(campo);
    }

    [Fact(DisplayName = "Validator recusa as quatro regras ausentes numa resposta só, na ordem")]
    public void Rejeita_QuatroRegrasAusentes_Acumula()
    {
        DefinirClassificacaoCommand comando = ComandoValido([]) with
        {
            RegraCalculoCodigo = null,
            RegraCalculoVersao = null,
            RegraOrdemAlocacaoCodigo = null,
            RegraOrdemAlocacaoVersao = null,
        };

        ValidationResult result = new DefinirClassificacaoCommandValidator().Validate(comando);

        result.Errors.Select(static e => e.PropertyName).Should().Equal(
            "RegraCalculoCodigo", "RegraCalculoVersao", "RegraOrdemAlocacaoCodigo", "RegraOrdemAlocacaoVersao");
    }

    [Fact(DisplayName = "Validator deixa a lista de eliminações ausente para o domínio recusar como obrigatória")]
    public void Aceita_RegrasEliminacaoNula_ARecusaEDoDominio()
    {
        ValidationResult result = new DefinirClassificacaoCommandValidator().Validate(ComandoValido(null!));

        result.IsValid.Should().BeTrue();
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
