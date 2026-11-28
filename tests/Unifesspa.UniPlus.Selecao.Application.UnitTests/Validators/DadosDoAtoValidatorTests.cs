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

    [Theory(DisplayName = "Texto ausente, nulo ou em branco não é do validator: a conferência do ato o recusa com o código de campo obrigatório")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TextoAusenteOuEmBranco_NaoEDoValidator(string? texto) =>
        _validator.Validate(Valido() with { Orgao = texto, Serie = texto, Assinante = texto, TipoAtoCodigo = texto }).IsValid.Should().BeTrue();

    [Fact(DisplayName = "Órgão, série, assinante e tipo acima do tamanho do registro são recusados, cada um no seu campo")]
    public void TextoAcimaDoTamanho_Recusa()
    {
        string longo = new('a', 201);

        ValidationResult resultado = _validator.Validate(Valido() with { Orgao = longo, Serie = longo, Assinante = longo, TipoAtoCodigo = longo });

        resultado.Errors.Select(static e => e.PropertyName).Should().BeEquivalentTo(
            [nameof(DadosDoAto.Orgao), nameof(DadosDoAto.Serie), nameof(DadosDoAto.Assinante), nameof(DadosDoAto.TipoAtoCodigo)]);
    }

    [Fact(DisplayName = "O catálogo confere o formato do tipo de ato e recusa tipo sem versão vigente")]
    public void Validar_TipoForaDoFormato_Aceita() =>
        _validator.Validate(Valido() with { TipoAtoCodigo = "aviso" }).IsValid.Should().BeTrue();
}
