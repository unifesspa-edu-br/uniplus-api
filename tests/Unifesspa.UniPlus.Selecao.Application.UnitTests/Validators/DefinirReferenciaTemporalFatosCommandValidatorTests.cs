namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Validators;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura de <see cref="DefinirReferenciaTemporalFatosCommandValidator"/> (Story #554,
/// issue #892, PR-b) — inclui a regressão do achado Codex P2 (PR #896): <c>Tipo</c> nulo
/// (remoção) não pode carregar <c>Data</c>/<c>FaseId</c> soltos.
/// </summary>
public sealed class DefinirReferenciaTemporalFatosCommandValidatorTests
{
    private static ValidationResult Validar(string? tipo, DateOnly? data, Guid? faseId) =>
        new DefinirReferenciaTemporalFatosCommandValidator().Validate(
            new DefinirReferenciaTemporalFatosCommand(Guid.CreateVersion7(), tipo, data, faseId, PrecondicaoIfMatch.Ausente));

    [Fact(DisplayName = "Tipo nulo sem Data nem FaseId é aceito — remoção da referência")]
    public void Aceita_RemocaoSemCamposSoltos() =>
        Validar(null, null, null).IsValid.Should().BeTrue();

    [Fact(DisplayName = "Tipo válido com os campos coerentes é aceito")]
    public void Aceita_TipoValido() =>
        Validar("DATA_ESPECIFICA", new DateOnly(2026, 3, 1), null).IsValid.Should().BeTrue();

    [Fact(DisplayName = "Tipo fora do domínio é recusado")]
    public void Rejeita_TipoDesconhecido() =>
        Validar("TIPO_INEXISTENTE", null, null).IsValid.Should().BeFalse();

    [Fact(DisplayName = "Achado Codex P2 (PR #896): Tipo nulo com Data solta é recusado — não pode remover a referência silenciosamente")]
    public void Rejeita_TipoNuloComDataSolta()
    {
        ValidationResult resultado = Validar(null, new DateOnly(2026, 3, 1), null);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.ErrorMessage.Contains("Data e FaseId não são aceitos", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Achado Codex P2 (PR #896): Tipo nulo com FaseId solta é recusado")]
    public void Rejeita_TipoNuloComFaseIdSolta()
    {
        ValidationResult resultado = Validar(null, null, Guid.CreateVersion7());

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.ErrorMessage.Contains("Data e FaseId não são aceitos", StringComparison.Ordinal));
    }
}
