namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Validators;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// UNI-REQ-0081: o valor congelado do par de suspensividade é sempre estritamente
/// positivo — zero ou negativo tornaria o prazo inutilizável desde a publicação
/// (imutável). Ausência (null) já é a forma correta de desativar uma instância.
/// </summary>
public sealed class DefinirCronogramaFasesCommandValidatorTests
{
    private static RegraRecursoFaseInput RegraRecurso(
        decimal? susp1Valor = null,
        UnidadePrazo? susp1Unidade = null,
        decimal? susp2Valor = null,
        UnidadePrazo? susp2Unidade = null) => new(
            RegraCodigo: RegraPrazoRecursoCodigo.AncoradoEmAto,
            RegraVersao: "v1",
            PrazoValor: 48m,
            PrazoUnidade: UnidadePrazo.Horas,
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

    [Fact(DisplayName = "Lista de fases vazia atravessa a validação de forma — quem recusa é o domínio")]
    public void Aceita_ListaDeFasesVazia_ParaODominioRecusar()
    {
        DefinirCronogramaFasesCommand comando = new(Guid.CreateVersion7(), [], PrecondicaoIfMatch.Ausente);

        ValidationResult resultado = new DefinirCronogramaFasesCommandValidator().Validate(comando);

        resultado.Errors.Should().NotContain(
            e => e.PropertyName == nameof(DefinirCronogramaFasesCommand.Fases),
            "ProcessoSeletivo.CronogramaFasesVazio é a causa nomeada, e recusar aqui a tornava inalcançável");
    }

    [Fact(DisplayName = "Aceita suspensividade ausente (null) — é a forma de desativar uma instância")]
    public void Aceita_SuspensividadeAusente()
    {
        ValidationResult resultado = new DefinirCronogramaFasesCommandValidator()
            .Validate(Comando(RegraRecurso()));

        resultado.IsValid.Should().BeTrue(string.Join("; ", resultado.Errors));
    }

    [Fact(DisplayName = "Aceita suspensividade positiva")]
    public void Aceita_SuspensividadePositiva()
    {
        ValidationResult resultado = new DefinirCronogramaFasesCommandValidator()
            .Validate(Comando(RegraRecurso(susp1Valor: 5m, susp1Unidade: UnidadePrazo.Dias)));

        resultado.IsValid.Should().BeTrue(string.Join("; ", resultado.Errors));
    }

    [Theory(DisplayName = "Validator passa com suspensividade não positiva ou pela metade — a recusa é de RegraRecursoFase.Criar, fonte única de validação (ADR-0125)")]
    [InlineData(0, UnidadePrazo.Dias)]
    [InlineData(-1, UnidadePrazo.Dias)]
    [InlineData(5, UnidadePrazo.Nenhuma)]
    public void Aceita_SuspensividadeInvalidaNoValidator(int valor, UnidadePrazo unidade)
    {
        ValidationResult resultado = new DefinirCronogramaFasesCommandValidator()
            .Validate(Comando(RegraRecurso(susp1Valor: valor, susp1Unidade: unidade)));

        resultado.IsValid.Should().BeTrue(
            "magnitude e unidade declarável são invariantes do agregado; repeti-las aqui devolveria a resposta "
            + "genérica do validator no lugar do erro de negócio nomeado — a cobertura vive em RegraRecursoFaseTests");
    }

    [Fact(DisplayName = "Validator passa com janela invertida — a rejeição é do agregado (FaseCronograma.Criar, ADR-0125), para poder acumular com outras violações da mesma fase")]
    public void Aceita_JanelaInvertidaNoValidator()
    {
        FaseCronogramaInput fase = new(
            Ordem: 1,
            FaseCanonicaId: Guid.CreateVersion7(),
            Inicio: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            Fim: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            AtoProduzidoCodigo: null,
            TiposBancaIds: [],
            RegraRecurso: null);
        DefinirCronogramaFasesCommand command = new(Guid.CreateVersion7(), [fase], PrecondicaoIfMatch.Ausente);

        ValidationResult resultado = new DefinirCronogramaFasesCommandValidator().Validate(command);

        resultado.IsValid.Should().BeTrue(string.Join("; ", resultado.Errors));
    }
}
