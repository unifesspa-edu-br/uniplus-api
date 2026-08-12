namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura de <see cref="RegraRecursoFase.Criar"/> (Story #851 §3.6): as invariantes
/// puras que o VO consegue provar sozinho — referência por símbolo (CA-01/CA-02). A
/// resolução contra o catálogo vivo (existe, TipoRegra correto, hash bate) e a checagem
/// de calendário vigente/localidade para DIAS_UTEIS (issue #1113) são do handler
/// (Application) — ver <c>DefinirCronogramaFasesCommandHandlerTests</c>; este VO aceita
/// DIAS_UTEIS estruturalmente, como qualquer outra <see cref="UnidadePrazo"/>.
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

    [Fact(DisplayName = "CA-03 (contraprova): domínio ACEITA DiasUteis estruturalmente — a checagem de calendário vigente/localidade é do handler, não daqui")]
    public void PrazoEmDiasUteis_AceitoEstruturalmente()
    {
        ReferenciaRegra regra = ReferenciaRegra.Criar(
            RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('a', 64)).Value!;

        Result<RegraRecursoFase> resultado = RegraRecursoFase.Criar(regra, ArgsBase(prazoUnidade: UnidadePrazo.DiasUteis));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Args.PrazoUnidade.Should().Be(UnidadePrazo.DiasUteis);
    }

    [Theory(DisplayName = "CA-03 (contraprova): suspensividade em DiasUteis, em qualquer uma das duas instâncias, é aceita estruturalmente pelo domínio")]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void SuspensividadeEmDiasUteis_AceitaEstruturalmente(bool primeiraEmDiasUteis, bool segundaEmDiasUteis)
    {
        ReferenciaRegra regra = ReferenciaRegra.Criar(
            RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('a', 64)).Value!;

        UnidadePrazo? susp1 = primeiraEmDiasUteis ? UnidadePrazo.DiasUteis : UnidadePrazo.Dias;
        UnidadePrazo? susp2 = segundaEmDiasUteis ? UnidadePrazo.DiasUteis : UnidadePrazo.Dias;

        Result<RegraRecursoFase> resultado = RegraRecursoFase.Criar(
            regra, ArgsBase(susp1Unidade: susp1, susp2Unidade: susp2));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Args.SuspensividadePrimeiraInstanciaUnidade.Should().Be(susp1);
        resultado.Value.Args.SuspensividadeSegundaInstanciaUnidade.Should().Be(susp2);
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
