namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class CalculadoraQuadroVagasLei12711Tests
{
    private static readonly IReadOnlyDictionary<string, int> SemRetiradas = new Dictionary<string, int>();
    private static readonly IReadOnlyDictionary<string, int> SemSuplementares = new Dictionary<string, int>();

    private static ReferenciaReservaDemograficaSnapshot Demografica(decimal ppi, decimal quilombola, decimal pcd) =>
        ReferenciaReservaDemograficaSnapshot.Criar(Guid.CreateVersion7(), "2022", ppi, quilombola, pcd, "Censo 2022").Value!;

    [Fact(DisplayName = "V1/CA-02 — caso limpo sem estouro: VO=40, PR=0,5, ppi=50, q=10, pcd=10")]
    public void Calcula_QuadroLimpo_SemEstouro()
    {
        Result<QuadroVagasCalculado> resultado = CalculadoraQuadroVagasLei12711.Calcular(
            40, 0.5m, Demografica(50, 10, 10), SemRetiradas, SemSuplementares);

        resultado.IsSuccess.Should().BeTrue();
        QuadroVagasCalculado quadro = resultado.Value!;
        quadro.Quadro[ModalidadesFederaisLei12711.LbPpi].Should().Be(5);
        quadro.Quadro[ModalidadesFederaisLei12711.LbQ].Should().Be(1);
        quadro.Quadro[ModalidadesFederaisLei12711.LbPcd].Should().Be(1);
        quadro.Quadro[ModalidadesFederaisLei12711.LbEp].Should().Be(3);
        quadro.Quadro[ModalidadesFederaisLei12711.LiPpi].Should().Be(5);
        quadro.Quadro[ModalidadesFederaisLei12711.LiQ].Should().Be(1);
        quadro.Quadro[ModalidadesFederaisLei12711.LiPcd].Should().Be(1);
        quadro.Quadro[ModalidadesFederaisLei12711.LiEp].Should().Be(3);
        quadro.Ac.Should().Be(20);
        quadro.Estouro.Should().Be(0);
        quadro.CapadoEmVo.Should().BeFalse();
        quadro.TotalPublicado.Should().Be(40);
    }

    [Fact(DisplayName = "V2/CA-03 — LI_Q arredonda por piso, não por teto: VO=48, PR=0,5, q=15")]
    public void LiQ_ArredondaPorPiso_NaoPorTeto()
    {
        Result<QuadroVagasCalculado> resultado = CalculadoraQuadroVagasLei12711.Calcular(
            48, 0.5m, Demografica(50, 15, 10), SemRetiradas, SemSuplementares);

        resultado.IsSuccess.Should().BeTrue();
        // VRSI=12; ceil(12*0,15)=2, floor(12*0,15)=1 — o piso é o correto.
        resultado.Value!.Quadro[ModalidadesFederaisLei12711.LiQ].Should().Be(1);
    }

    [Fact(DisplayName = "V3 — mín-1 ordenada eleva LB_Q 0→1; LI_Q permanece 0 (fora da ordem)")]
    public void MinimoUmaVaga_ElevaGrupoZerado_LiQForaDaOrdem()
    {
        Result<QuadroVagasCalculado> resultado = CalculadoraQuadroVagasLei12711.Calcular(
            40, 0.5m, Demografica(50, 0, 10), SemRetiradas, SemSuplementares);

        resultado.IsSuccess.Should().BeTrue();
        QuadroVagasCalculado quadro = resultado.Value!;
        quadro.Quadro[ModalidadesFederaisLei12711.LbQ].Should().Be(1);
        quadro.Quadro[ModalidadesFederaisLei12711.LiQ].Should().Be(0);
        quadro.VrFinal.Should().Be(21);
        quadro.Ac.Should().Be(19);
        quadro.Estouro.Should().Be(1);
    }

    [Theory(DisplayName = "V4/INV-1 — PR fora de [0,5; 1] não é responsabilidade da calculadora (contrato do chamador)")]
    [InlineData(0.6, 24)]
    public void Pr_EscalaVrNominal(double pr, int vrEsperado)
    {
        Result<QuadroVagasCalculado> resultado = CalculadoraQuadroVagasLei12711.Calcular(
            40, (decimal)pr, Demografica(50, 10, 10), SemRetiradas, SemSuplementares);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.VrNominal.Should().Be(vrEsperado);
    }

    [Fact(DisplayName = "V5/CA-05 — resíduo nunca negativo: VO=50, PR=0,5, ppi=79, q=1,5, pcd=8,5")]
    public void ResiduoEp_NuncaNegativo()
    {
        Result<QuadroVagasCalculado> resultado = CalculadoraQuadroVagasLei12711.Calcular(
            50, 0.5m, Demografica(79, 1.5m, 8.5m), SemRetiradas, SemSuplementares);

        resultado.IsSuccess.Should().BeTrue();
        QuadroVagasCalculado quadro = resultado.Value!;
        quadro.Quadro[ModalidadesFederaisLei12711.LbEp].Should().BeGreaterThanOrEqualTo(0);
        quadro.Quadro[ModalidadesFederaisLei12711.LiEp].Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact(DisplayName = "V5/CA-06 — estouro é registrado, nunca silenciado: retirada de 2 em V")]
    public void Estouro_EhRegistrado_ComIndicadorDeCapagem()
    {
        Dictionary<string, int> retiradas = new(StringComparer.Ordinal) { ["V"] = 2 };

        Result<QuadroVagasCalculado> resultado = CalculadoraQuadroVagasLei12711.Calcular(
            50, 0.5m, Demografica(79, 1.5m, 8.5m), retiradas, SemSuplementares);

        resultado.IsSuccess.Should().BeTrue();
        QuadroVagasCalculado quadro = resultado.Value!;
        quadro.VrFinal.Should().Be(28);
        quadro.VrNominal.Should().Be(25);
        quadro.Estouro.Should().Be(3);
        quadro.Ac.Should().Be(20);
        quadro.TotalPublicado.Should().Be(50);
    }

    [Fact(DisplayName = "V6/CA-07 — ampla concorrência negativa bloqueia: VO=10, PR=0,8, V retira 5")]
    public void AmplaConcorrenciaNegativa_Falha()
    {
        Dictionary<string, int> retiradas = new(StringComparer.Ordinal) { ["V"] = 5 };

        Result<QuadroVagasCalculado> resultado = CalculadoraQuadroVagasLei12711.Calcular(
            10, 0.8m, Demografica(50, 10, 10), retiradas, SemSuplementares);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.QuadroAmplaConcorrenciaNegativa");
    }

    [Fact(DisplayName = "CA-08 — conservação: Σ(sub-reservas) + Σ(retiradas) + AC = VO, em todo vetor calculado")]
    public void Quadro_ConservaVagasOfertadas()
    {
        Dictionary<string, int> retiradas = new(StringComparer.Ordinal) { ["V"] = 2 };
        int voBase = 50;

        Result<QuadroVagasCalculado> resultado = CalculadoraQuadroVagasLei12711.Calcular(
            voBase, 0.5m, Demografica(79, 1.5m, 8.5m), retiradas, SemSuplementares);

        resultado.IsSuccess.Should().BeTrue();
        QuadroVagasCalculado quadro = resultado.Value!;
        (quadro.VrFinal + quadro.RetiradasTotal + quadro.Ac).Should().Be(voBase);
        quadro.TotalPublicado.Should().Be(voBase + quadro.SuplementaresTotal);
    }

    [Fact(DisplayName = "V7/CA-09 — retirada com código de sub-reserva federal colide e é recusada")]
    public void RetiradaComCodigoDeSubReserva_Falha()
    {
        Dictionary<string, int> retiradas = new(StringComparer.Ordinal) { [ModalidadesFederaisLei12711.Ac] = 2 };

        Result<QuadroVagasCalculado> resultado = CalculadoraQuadroVagasLei12711.Calcular(
            40, 0.5m, Demografica(50, 10, 10), retiradas, SemSuplementares);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.QuadroChaveColide");
    }

    [Fact(DisplayName = "V8 — art. 11 §único: curso pequeno (VO=3) capa com prioridade LB, sem bloqueio")]
    public void CursoPequeno_PreenchePisoNaOrdemLegal_ECapaEmVo()
    {
        Result<QuadroVagasCalculado> resultado = CalculadoraQuadroVagasLei12711.Calcular(
            3, 0.5m, Demografica(79, 1.5m, 8.5m), SemRetiradas, SemSuplementares);

        resultado.IsSuccess.Should().BeTrue();
        QuadroVagasCalculado quadro = resultado.Value!;
        quadro.Quadro[ModalidadesFederaisLei12711.LbPpi].Should().Be(1);
        quadro.Quadro[ModalidadesFederaisLei12711.LbQ].Should().Be(1);
        quadro.Quadro[ModalidadesFederaisLei12711.LbPcd].Should().Be(1);
        quadro.Quadro[ModalidadesFederaisLei12711.LbEp].Should().Be(0);
        quadro.Quadro[ModalidadesFederaisLei12711.LiPpi].Should().Be(0);
        quadro.CapadoEmVo.Should().BeTrue();
        quadro.VrFinal.Should().Be(3);
        quadro.Ac.Should().Be(0);
    }

    [Fact(DisplayName = "V9 — VO=2 capa só na ordem I,II (LB_PPI=1, LB_Q=1), resto 0")]
    public void VoMenorQueGruposGarantidos_CapaSoNaOrdemQueCabe()
    {
        Result<QuadroVagasCalculado> resultado = CalculadoraQuadroVagasLei12711.Calcular(
            2, 0.5m, Demografica(79, 1.5m, 8.5m), SemRetiradas, SemSuplementares);

        resultado.IsSuccess.Should().BeTrue();
        QuadroVagasCalculado quadro = resultado.Value!;
        quadro.Quadro[ModalidadesFederaisLei12711.LbPpi].Should().Be(1);
        quadro.Quadro[ModalidadesFederaisLei12711.LbQ].Should().Be(1);
        quadro.Quadro[ModalidadesFederaisLei12711.LbPcd].Should().Be(0);
        quadro.CapadoEmVo.Should().BeTrue();
    }

    [Fact(DisplayName = "V10 — cap é condicional à escassez: VO=50 não é capado")]
    public void CapCondicionalAEscassez_CursoGrandeNaoCapa()
    {
        Result<QuadroVagasCalculado> resultado = CalculadoraQuadroVagasLei12711.Calcular(
            50, 0.5m, Demografica(79, 1.5m, 8.5m), SemRetiradas, SemSuplementares);

        resultado.IsSuccess.Should().BeTrue();
        QuadroVagasCalculado quadro = resultado.Value!;
        quadro.CapadoEmVo.Should().BeFalse();
        quadro.VrFinal.Should().Be(28);
    }

    [Fact(DisplayName = "V11 — floor-first: piso de I,II,III vence o excedente de I (achado Codex do protótipo)")]
    public void FloorFirst_PisoDosGruposPosterioresVenceExcedenteDoAnterior_CasoSimetrico()
    {
        Result<QuadroVagasCalculado> resultado = CalculadoraQuadroVagasLei12711.Calcular(
            3, 1m, Demografica(100, 0, 0), SemRetiradas, SemSuplementares);

        resultado.IsSuccess.Should().BeTrue();
        QuadroVagasCalculado quadro = resultado.Value!;
        quadro.Quadro[ModalidadesFederaisLei12711.LbPpi].Should().Be(1, "o piso de LB_Q e LB_PCD vence o 2º de LB_PPI");
        quadro.Quadro[ModalidadesFederaisLei12711.LbQ].Should().Be(1);
        quadro.Quadro[ModalidadesFederaisLei12711.LbPcd].Should().Be(1);
        quadro.Quadro[ModalidadesFederaisLei12711.LiPpi].Should().Be(0);
        quadro.VrFinal.Should().Be(3);
        quadro.CapadoEmVo.Should().BeTrue();
    }

    [Fact(DisplayName = "V12 — floor-first: mesmo achado, vetor irmão com percentuais assimétricos")]
    public void FloorFirst_PisoDosGruposPosterioresVenceExcedenteDoAnterior_CasoAssimetrico()
    {
        Result<QuadroVagasCalculado> resultado = CalculadoraQuadroVagasLei12711.Calcular(
            3, 1m, Demografica(79, 1.5m, 8.5m), SemRetiradas, SemSuplementares);

        resultado.IsSuccess.Should().BeTrue();
        QuadroVagasCalculado quadro = resultado.Value!;
        quadro.Quadro[ModalidadesFederaisLei12711.LbPpi].Should().Be(1);
        quadro.Quadro[ModalidadesFederaisLei12711.LbQ].Should().Be(1);
        quadro.Quadro[ModalidadesFederaisLei12711.LbPcd].Should().Be(1);
        quadro.Quadro[ModalidadesFederaisLei12711.LbEp].Should().Be(0);
        quadro.VrFinal.Should().Be(3);
        quadro.CapadoEmVo.Should().BeTrue();
    }

    [Fact(DisplayName = "V13 — quantidade negativa de retirada é recusada")]
    public void QuantidadeNegativaDeRetirada_Falha()
    {
        Dictionary<string, int> retiradas = new(StringComparer.Ordinal) { ["V"] = -1 };

        Result<QuadroVagasCalculado> resultado = CalculadoraQuadroVagasLei12711.Calcular(
            40, 0.5m, Demografica(50, 10, 10), retiradas, SemSuplementares);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.QuantidadeVagaNegativa");
    }

    [Fact(DisplayName = "Suplemento aumenta o total publicado sem afetar VO nem a ampla concorrência")]
    public void Suplemento_AumentaTotalPublicado_SemAfetarAc()
    {
        Dictionary<string, int> suplementares = new(StringComparer.Ordinal) { ["CONV_MUNICIPAL"] = 3 };

        Result<QuadroVagasCalculado> resultado = CalculadoraQuadroVagasLei12711.Calcular(
            40, 0.5m, Demografica(50, 10, 10), SemRetiradas, suplementares);

        resultado.IsSuccess.Should().BeTrue();
        QuadroVagasCalculado quadro = resultado.Value!;
        quadro.Ac.Should().Be(20);
        quadro.SuplementaresTotal.Should().Be(3);
        quadro.TotalPublicado.Should().Be(43);
    }
}
