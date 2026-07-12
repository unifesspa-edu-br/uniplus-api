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

    [Fact(DisplayName = "Ano omitido do corpo binda para 0 e é recusado — não pode publicar e falhar depois")]
    public void AnoAusente_Recusa()
    {
        ValidationResult resultado = _validator.Validate(Valido() with { Ano = 0 });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(DadosDoAto.Ano));
    }

    [Fact(DisplayName = "Data de publicação omitida binda para 0001-01-01 e é recusada")]
    public void DataAusente_Recusa()
    {
        ValidationResult resultado = _validator.Validate(Valido() with { DataPublicacao = default });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(DadosDoAto.DataPublicacao));
    }

    [Theory(DisplayName = "Órgão e assinante em branco são recusados")]
    [InlineData("")]
    [InlineData("   ")]
    public void OrgaoEmBranco_Recusa(string orgao)
    {
        _validator.Validate(Valido() with { Orgao = orgao }).IsValid.Should().BeFalse();
        _validator.Validate(Valido() with { Assinante = orgao }).IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "Tipo do ato fora da forma canônica é recusado — mas o VALOR não é validado (é cadastro, ADR-0103)")]
    public void TipoForaDaFormaCanonica_Recusa()
    {
        _validator.Validate(Valido() with { TipoAtoCodigo = "edital_abertura" }).IsValid.Should().BeFalse();

        // Um tipo que este código nunca ouviu falar passa: a lista vive no cadastro de
        // Publicações, e conhecê-la aqui seria ramificar por tipo — o que a ADR-0103 proíbe.
        _validator.Validate(Valido() with { TipoAtoCodigo = "CONVOCACAO_HETEROIDENTIFICACAO" })
            .IsValid.Should().BeTrue();
    }
}
