namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Validators;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

/// <summary>
/// O bloco documental do ato é recusado ANTES de a publicação ser gravada.
/// </summary>
/// <remarks>
/// O registro do ato acontece depois, por mensagem durável (ADR-0108). Sem estas regras, um
/// ano ausente ou um órgão em branco passariam pela publicação — o Edital seria gravado, o
/// cliente receberia 204 — e a requisição só morreria na dead letter, deixando Seleção
/// publicada sem o ato. O que o formato pode recusar tem de virar 422 na hora.
/// </remarks>
public sealed class DadosDoAtoValidatorTests
{
    private static DadosDoAto Valido() => new(
        "CEPS", "EDITAL", 2026, new DateOnly(2026, 3, 13), "Diretor do CEPS", "EDITAL_ABERTURA");

    private readonly DadosDoAtoValidator _validator = new();

    [Fact(DisplayName = "Bloco do ato completo é aceito")]
    public void Valido_Aceita()
    {
        _validator.Validate(Valido()).IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Ano informado como zero é recusado pela faixa — não pode publicar e falhar depois")]
    public void AnoZero_Recusa()
    {
        ValidationResult resultado = _validator.Validate(Valido() with { Ano = 0 });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(DadosDoAto.Ano));
    }

    [Fact(DisplayName = "Data informada como 0001-01-01 é recusada")]
    public void DataZero_Recusa()
    {
        ValidationResult resultado = _validator.Validate(Valido() with { DataDoAto = default(DateOnly) });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(DadosDoAto.DataDoAto));
    }

    [Fact(DisplayName = "Ano e data ausentes não são do validator: a conferência do ato os recusa com o código de campo obrigatório")]
    public void AnoEDataAusentes_NaoSaoDoValidator() =>
        _validator.Validate(Valido() with { Ano = null, DataDoAto = null }).IsValid.Should().BeTrue();

    [Theory(DisplayName = "Órgão e assinante em branco são recusados")]
    [InlineData("")]
    [InlineData("   ")]
    public void OrgaoEmBranco_Recusa(string orgao)
    {
        _validator.Validate(Valido() with { Orgao = orgao }).IsValid.Should().BeFalse();
        _validator.Validate(Valido() with { Assinante = orgao }).IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "O catálogo confere o formato do tipo de ato e recusa tipo sem versão vigente")]
    public void Validar_TipoForaDoFormato_Aceita() =>
        _validator.Validate(Valido() with { TipoAtoCodigo = "aviso" }).IsValid.Should().BeTrue();
}
