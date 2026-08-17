namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Cobertura de <see cref="RegraDerivacaoConfigurada.Criar"/> (Story #927, ADR-0125) —
/// ordem e contribuição, e de <see cref="RegraDerivacaoConfigurada.ValidarFormaBasica"/> (a
/// checagem pura, sem condições resolvidas, que o handler roda antes do vocabulário).
/// </summary>
public sealed class RegraDerivacaoConfiguradaTests
{
    [Fact(DisplayName = "Criar com ordem e contribuição válidas é aceito (regra âncora, sem condições)")]
    public void Criar_RegraAncora_Aceita()
    {
        Result<RegraDerivacaoConfigurada> resultado = RegraDerivacaoConfigurada.Criar(0, "AC", null);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Contribui.Should().Be("AC");
    }

    [Fact(DisplayName = "Criar com ordem negativa falha")]
    public void Criar_OrdemNegativa_Recusa()
    {
        Result<RegraDerivacaoConfigurada> resultado = RegraDerivacaoConfigurada.Criar(-1, "AC", null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(RegraDerivacaoConfiguradaErrorCodes.OrdemInvalida);
    }

    [Fact(DisplayName = "Criar com contribuição vazia falha")]
    public void Criar_ContribuiVazio_Recusa()
    {
        Result<RegraDerivacaoConfigurada> resultado = RegraDerivacaoConfigurada.Criar(0, "", null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(RegraDerivacaoConfiguradaErrorCodes.ContribuiObrigatorio);
    }

    [Fact(DisplayName = "ADR-0125: ordem negativa e contribuição vazia acumulam no mesmo lote")]
    public void Criar_OrdemNegativaEContribuiVazio_AcumulaAsDuasViolacoes()
    {
        Result<RegraDerivacaoConfigurada> resultado = RegraDerivacaoConfigurada.Criar(-1, "", null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
        [
            RegraDerivacaoConfiguradaErrorCodes.OrdemInvalida,
            RegraDerivacaoConfiguradaErrorCodes.ContribuiObrigatorio,
        ]);
    }

    [Fact(DisplayName = "ValidarFormaBasica sem violações retorna lote vazio")]
    public void ValidarFormaBasica_SemViolacoes_Vazio()
    {
        List<FieldError> erros = RegraDerivacaoConfigurada.ValidarFormaBasica(0, "AC");

        erros.Should().BeEmpty();
    }
}
