namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura de <see cref="RegraRecursoFase.Criar"/> (Story #851 §3.6): as invariantes
/// puras que o VO consegue provar sozinho — referência por símbolo (CA-01/CA-02) e
/// DIAS_UTEIS sem calendário na interposição e na suspensividade (CA-20/CA-21). A
/// resolução contra o catálogo vivo (existe, TipoRegra correto, hash bate) é do
/// handler (Application) — ver <c>DefinirCronogramaFasesCommandHandlerTests</c>.
/// </summary>
public sealed class RegraRecursoFaseTests
{
    private static ArgsRegraPrazoRecurso ArgsBase(
        UnidadePrazo prazoUnidade = UnidadePrazo.Horas,
        UnidadePrazo? susp1Unidade = null,
        UnidadePrazo? susp2Unidade = null) => new(
            PrazoValor: 48m,
            PrazoUnidade: prazoUnidade,
            AtoAncoraCodigo: "RESULTADO_PRELIMINAR",
            SuspensividadePrimeiraInstanciaValor: susp1Unidade is null ? null : 5m,
            SuspensividadePrimeiraInstanciaUnidade: susp1Unidade,
            SuspensividadeSegundaInstanciaValor: susp2Unidade is null ? null : 5m,
            SuspensividadeSegundaInstanciaUnidade: susp2Unidade);

    [Fact(DisplayName = "CA-01: referencia a regra por SÍMBOLO (RegraPrazoRecursoCodigo.AncoradoEmAto), não literal solto")]
    public void ReferenciaRegraPorSimbolo()
    {
        ReferenciaRegra regra = ReferenciaRegra.Criar(
            RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('a', 64)).Value!;

        Result<RegraRecursoFase> resultado = RegraRecursoFase.Criar(regra, ArgsBase());

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Regra.Codigo.Should().Be(RegraPrazoRecursoCodigo.AncoradoEmAto);
    }

    [Fact(DisplayName = "CA-02 (contraprova): referenciar qualquer OUTRA regra do catálogo é recusado")]
    public void RegraDeTipoIncompativel_Recusa()
    {
        ReferenciaRegra outraRegra = ReferenciaRegra.Criar(
            "BONUS-MULTIPLICATIVO", "v1", new string('b', 64)).Value!;

        Result<RegraRecursoFase> resultado = RegraRecursoFase.Criar(outraRegra, ArgsBase());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraRecursoFase.RegraCatalogoInvalida");
    }

    [Fact(DisplayName = "CA-20: prazo de interposição em DIAS_UTEIS é recusado — nunca aproximado em silêncio")]
    public void PrazoEmDiasUteis_SemCalendario_Recusa()
    {
        ReferenciaRegra regra = ReferenciaRegra.Criar(
            RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('a', 64)).Value!;

        Result<RegraRecursoFase> resultado = RegraRecursoFase.Criar(regra, ArgsBase(prazoUnidade: UnidadePrazo.DiasUteis));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraRecursoFase.PrazoEmDiasUteisSemCalendario");
    }

    [Theory(DisplayName = "CA-21: suspensividade em DIAS_UTEIS é recusada em QUALQUER uma das duas instâncias, independentemente")]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void SuspensividadeEmDiasUteis_QualquerInstancia_Recusa(bool primeiraEmDiasUteis, bool segundaEmDiasUteis)
    {
        ReferenciaRegra regra = ReferenciaRegra.Criar(
            RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('a', 64)).Value!;

        UnidadePrazo? susp1 = primeiraEmDiasUteis ? UnidadePrazo.DiasUteis : UnidadePrazo.Dias;
        UnidadePrazo? susp2 = segundaEmDiasUteis ? UnidadePrazo.DiasUteis : UnidadePrazo.Dias;

        Result<RegraRecursoFase> resultado = RegraRecursoFase.Criar(
            regra, ArgsBase(susp1Unidade: susp1, susp2Unidade: susp2));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraRecursoFase.SuspensividadeEmDiasUteisSemCalendario");
    }

    [Fact(DisplayName = "CA-21 (contraprova): suspensividade em DIAS corridos é aceita e congelada — em qualquer uma das duas instâncias, inclusive com a outra nula")]
    public void Suspensividade_DiasCorridos_PrimeiraPreenchidaSegundaNula_Aceita()
    {
        ReferenciaRegra regra = ReferenciaRegra.Criar(
            RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('a', 64)).Value!;

        Result<RegraRecursoFase> resultado = RegraRecursoFase.Criar(
            regra, ArgsBase(susp1Unidade: UnidadePrazo.Dias, susp2Unidade: null));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Args.SuspensividadePrimeiraInstanciaUnidade.Should().Be(UnidadePrazo.Dias);
        resultado.Value.Args.SuspensividadePrimeiraInstanciaValor.Should().Be(5m);
        resultado.Value.Args.SuspensividadeSegundaInstanciaUnidade.Should().BeNull(
            "null numa instância é valor legítimo — significa que ela não bloqueia (caso normal do Ingresso via judicial)");
        resultado.Value.Args.SuspensividadeSegundaInstanciaValor.Should().BeNull();
    }
}
