namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Validators;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Issue #1113: as regras de forma que a borda aplica exclusivamente a DIAS_UTEIS — sem
/// fração (contagem discreta) e com teto de magnitude (o domínio itera um dia por vez até
/// contar o valor declarado; sem teto, um valor gigantesco estoura o cast para
/// <c>int</c> ou gira por tempo inaceitável). Cobertura no NÍVEL do validator: os testes
/// do handler chamam <c>Handle</c> diretamente e não exercitam o pipeline
/// <c>UseFluentValidation</c> do Wolverine, então uma regressão aqui não apareceria lá.
/// </summary>
public sealed class DefinirCronogramaFasesCommandValidatorTests
{
    private static RegraRecursoFaseInput RegraRecurso(
        decimal prazoValor,
        UnidadePrazo prazoUnidade,
        decimal? susp1Valor = null,
        UnidadePrazo? susp1Unidade = null,
        decimal? susp2Valor = null,
        UnidadePrazo? susp2Unidade = null) => new(
            RegraCodigo: RegraPrazoRecursoCodigo.AncoradoEmAto,
            RegraVersao: "v1",
            PrazoValor: prazoValor,
            PrazoUnidade: prazoUnidade,
            AtoAncoraCodigo: "RESULTADO_PRELIMINAR",
            SuspensividadePrimeiraInstanciaValor: susp1Valor,
            SuspensividadePrimeiraInstanciaUnidade: susp1Unidade,
            SuspensividadeSegundaInstanciaValor: susp2Valor,
            SuspensividadeSegundaInstanciaUnidade: susp2Unidade);

    private static DefinirCronogramaFasesCommand Comando(RegraRecursoFaseInput regraRecurso) => new(
        Guid.CreateVersion7(),
        [
            new FaseCronogramaInput(
                Ordem: 1,
                FaseCanonicaId: Guid.CreateVersion7(),
                Inicio: null,
                Fim: null,
                AtoProduzidoCodigo: "RESULTADO_PRELIMINAR",
                TiposBancaIds: [],
                RegraRecurso: regraRecurso),
        ],
        PrecondicaoIfMatch.Ausente);

    [Fact(DisplayName = "Aceita prazo em DIAS_UTEIS inteiro dentro do teto")]
    public void Aceita_PrazoDiasUteisInteiro()
    {
        ValidationResult resultado = new DefinirCronogramaFasesCommandValidator()
            .Validate(Comando(RegraRecurso(3m, UnidadePrazo.DiasUteis)));

        resultado.IsValid.Should().BeTrue(string.Join("; ", resultado.Errors));
    }

    [Fact(DisplayName = "Rejeita prazo em DIAS_UTEIS fracionário")]
    public void Rejeita_PrazoDiasUteisFracionario()
    {
        ValidationResult resultado = new DefinirCronogramaFasesCommandValidator()
            .Validate(Comando(RegraRecurso(1.5m, UnidadePrazo.DiasUteis)));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == "Fases[0].RegraRecurso.PrazoValor");
    }

    [Fact(DisplayName = "Rejeita prazo em DIAS_UTEIS acima do teto de magnitude")]
    public void Rejeita_PrazoDiasUteisAcimaDoTeto()
    {
        ValidationResult resultado = new DefinirCronogramaFasesCommandValidator()
            .Validate(Comando(RegraRecurso(3651m, UnidadePrazo.DiasUteis)));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == "Fases[0].RegraRecurso.PrazoValor");
    }

    [Fact(DisplayName = "Aceita prazo fracionário quando a unidade é Horas — a regra só vale para DIAS_UTEIS")]
    public void Aceita_PrazoFracionarioEmHoras()
    {
        ValidationResult resultado = new DefinirCronogramaFasesCommandValidator()
            .Validate(Comando(RegraRecurso(1.5m, UnidadePrazo.Horas)));

        resultado.IsValid.Should().BeTrue(string.Join("; ", resultado.Errors));
    }

    [Fact(DisplayName = "Rejeita suspensividade da 1ª instância em DIAS_UTEIS fracionária")]
    public void Rejeita_Suspensividade1DiasUteisFracionaria()
    {
        ValidationResult resultado = new DefinirCronogramaFasesCommandValidator()
            .Validate(Comando(RegraRecurso(
                3m, UnidadePrazo.Horas, susp1Valor: 2.5m, susp1Unidade: UnidadePrazo.DiasUteis)));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == "Fases[0].RegraRecurso.SuspensividadePrimeiraInstanciaValor");
    }

    [Fact(DisplayName = "Rejeita suspensividade da 1ª instância em DIAS_UTEIS acima do teto")]
    public void Rejeita_Suspensividade1DiasUteisAcimaDoTeto()
    {
        ValidationResult resultado = new DefinirCronogramaFasesCommandValidator()
            .Validate(Comando(RegraRecurso(
                3m, UnidadePrazo.Horas, susp1Valor: 3651m, susp1Unidade: UnidadePrazo.DiasUteis)));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == "Fases[0].RegraRecurso.SuspensividadePrimeiraInstanciaValor");
    }

    [Fact(DisplayName = "Rejeita suspensividade da 2ª instância em DIAS_UTEIS fracionária")]
    public void Rejeita_Suspensividade2DiasUteisFracionaria()
    {
        ValidationResult resultado = new DefinirCronogramaFasesCommandValidator()
            .Validate(Comando(RegraRecurso(
                3m, UnidadePrazo.Horas, susp2Valor: 4.25m, susp2Unidade: UnidadePrazo.DiasUteis)));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == "Fases[0].RegraRecurso.SuspensividadeSegundaInstanciaValor");
    }

    [Fact(DisplayName = "Rejeita suspensividade da 2ª instância em DIAS_UTEIS acima do teto")]
    public void Rejeita_Suspensividade2DiasUteisAcimaDoTeto()
    {
        ValidationResult resultado = new DefinirCronogramaFasesCommandValidator()
            .Validate(Comando(RegraRecurso(
                3m, UnidadePrazo.Horas, susp2Valor: 999999m, susp2Unidade: UnidadePrazo.DiasUteis)));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == "Fases[0].RegraRecurso.SuspensividadeSegundaInstanciaValor");
    }

    [Fact(DisplayName = "Aceita suspensividade fracionária quando a unidade é Dias — a regra só vale para DIAS_UTEIS")]
    public void Aceita_SuspensividadeFracionariaEmDiasCorridos()
    {
        ValidationResult resultado = new DefinirCronogramaFasesCommandValidator()
            .Validate(Comando(RegraRecurso(
                3m, UnidadePrazo.Horas, susp1Valor: 5.5m, susp1Unidade: UnidadePrazo.Dias)));

        resultado.IsValid.Should().BeTrue(string.Join("; ", resultado.Errors));
    }
}
