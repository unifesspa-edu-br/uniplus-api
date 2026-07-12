namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Validators;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

/// <summary>
/// Cobertura do <see cref="RetificarProcessoSeletivoCommandValidator"/>
/// (Story #759 T5 #786) — motivo obrigatório (ADR-0101), período válido e
/// referências obrigatórias mapeadas a 422 no boundary, antes do handler.
/// </summary>
public sealed class RetificarProcessoSeletivoCommandValidatorTests
{
    private static readonly RetificarProcessoSeletivoCommandValidator Validator = new();

    private static RetificarProcessoSeletivoCommand ComandoValido() => new(
        ProcessoSeletivoId: Guid.CreateVersion7(),
        Motivo: "Correção do prazo de inscrição",
        Numero: "001/2026-R1",
        PeriodoInscricaoInicio: new DateOnly(2026, 1, 2),
        PeriodoInscricaoFim: new DateOnly(2026, 2, 1),
        Ato: new DadosDoAto("CEPS", "EDITAL", 2026, new DateOnly(2026, 1, 2), "Diretor do CEPS", "EDITAL_RETIFICACAO"),
        DocumentoEditalId: Guid.CreateVersion7());

    [Fact(DisplayName = "Comando válido passa na validação")]
    public void Valido_Passa()
    {
        ValidationResult resultado = Validator.Validate(ComandoValido());
        resultado.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Motivo vazio é recusado (ADR-0101)")]
    public void MotivoVazio_Recusado()
    {
        ValidationResult resultado = Validator.Validate(ComandoValido() with { Motivo = "" });
        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(RetificarProcessoSeletivoCommand.Motivo));
    }

    [Fact(DisplayName = "Motivo dentro do limite cru mas que estoura o limite após NFC é recusado")]
    public void MotivoQueExpandeComNfc_AcimaDoLimite_Recusado()
    {
        // U+0958 (DEVANAGARI QA) é composição-excluída: NFC o decompõe em dois code points
        // (U+0915 U+093C). 501 chars crus (≤ 1000) viram 1002 após NFC — o valor
        // efetivamente persistido estoura o limite.
        string motivoQueExpande = new('\u0958', 501);

        ValidationResult resultado = Validator.Validate(ComandoValido() with { Motivo = motivoQueExpande });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(RetificarProcessoSeletivoCommand.Motivo));
    }

    [Fact(DisplayName = "Motivo entre 1001 e 2000 chars é recusado — o ato em Publicações corta em 1000, e publicar antes de descobrir isso deixaria a retificação sem ato")]
    public void MotivoAcimaDoLimiteDoAto_Recusado()
    {
        // A coluna de Seleção aceitaria 2000, mas o mesmo motivo viaja para o ato normativo
        // (ADR-0108), cujo limite é 1000. Aceitar aqui publicaria o Edital com 204 e mataria
        // o registro do ato na dead letter: retificação publicada, ato nenhum.
        ValidationResult resultado = Validator.Validate(ComandoValido() with { Motivo = new string('a', 1500) });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(RetificarProcessoSeletivoCommand.Motivo));
    }

    [Fact(DisplayName = "Fim do período anterior ao início é recusado")]
    public void PeriodoInvertido_Recusado()
    {
        ValidationResult resultado = Validator.Validate(ComandoValido() with
        {
            PeriodoInscricaoInicio = new DateOnly(2026, 2, 1),
            PeriodoInscricaoFim = new DateOnly(2026, 1, 2),
        });
        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(RetificarProcessoSeletivoCommand.PeriodoInscricaoFim));
    }

    [Fact(DisplayName = "Período com defaults (campos omitidos) é recusado")]
    public void PeriodoDefault_Recusado()
    {
        ValidationResult resultado = Validator.Validate(ComandoValido() with
        {
            PeriodoInscricaoInicio = default,
            PeriodoInscricaoFim = default,
        });
        resultado.IsValid.Should().BeFalse();
    }
}
