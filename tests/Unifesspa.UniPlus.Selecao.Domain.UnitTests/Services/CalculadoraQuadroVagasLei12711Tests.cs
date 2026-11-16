namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class CalculadoraQuadroVagasLei12711Tests
{
    private static readonly IReadOnlyDictionary<string, int> SemRetiradas = new Dictionary<string, int>();
    private static readonly IReadOnlyDictionary<string, int> SemSuplementares = new Dictionary<string, int>();

    private static ReferenciaReservaDemograficaSnapshot Demografica(decimal ppi, decimal quilombola, decimal pcd) =>
        ReferenciaReservaDemograficaSnapshot.Criar(Guid.CreateVersion7(), "2022", ppi, quilombola, pcd, "Censo 2022").Value!;

    private static Dictionary<string, int> QuadroLimpoEsperado() => new(StringComparer.Ordinal)
    {
        [ModalidadesFederaisLei12711.LbPpi] = 5,
        [ModalidadesFederaisLei12711.LbQ] = 1,
        [ModalidadesFederaisLei12711.LbPcd] = 1,
        [ModalidadesFederaisLei12711.LbEp] = 3,
        [ModalidadesFederaisLei12711.LiPpi] = 5,
        [ModalidadesFederaisLei12711.LiQ] = 1,
        [ModalidadesFederaisLei12711.LiPcd] = 1,
        [ModalidadesFederaisLei12711.LiEp] = 3,
        [ModalidadesFederaisLei12711.Ac] = 20,
    };

    [Fact(DisplayName = "V1/CA-02 — caso limpo sem estouro: VO=40, PR=0,5, ppi=50, q=10, pcd=10")]
    public void Calcula_QuadroLimpo_SemEstouro()
    {
        Result<QuadroDeVagasDaLeiDeCotas> resultado = CalculadoraQuadroVagasLei12711.Calcular(
            40, 0.5m, Demografica(50, 10, 10), SemRetiradas, SemSuplementares);

        resultado.IsSuccess.Should().BeTrue();
        QuadroDeVagasDaLeiDeCotas quadro = resultado.Value!;
        quadro.Vagas.ToDictionary(static v => v.ModalidadeCodigo, static v => v.Quantidade).Should().BeEquivalentTo(QuadroLimpoEsperado());
        quadro.QuantidadeDe(ModalidadesFederaisLei12711.Ac).Should().Be(20);
        quadro.ConferenciaDaReservaLegal.ExcedenteSobreAReservaLegal.Should().Be(0);
        quadro.ConferenciaDaReservaLegal.VagasCortadasPorEscassez.Should().Be(0);
        quadro.TotalDeVagasDoEdital.Should().Be(40);
    }

    [Fact(DisplayName = "V2/CA-03 — LI_Q arredonda por piso, não por teto: VO=48, PR=0,5, q=15")]
    public void LiQ_ArredondaPorPiso_NaoPorTeto()
    {
        Result<QuadroDeVagasDaLeiDeCotas> resultado = CalculadoraQuadroVagasLei12711.Calcular(
            48, 0.5m, Demografica(50, 15, 10), SemRetiradas, SemSuplementares);

        resultado.IsSuccess.Should().BeTrue();
        // VRSI=12; ceil(12*0,15)=2, floor(12*0,15)=1 — o piso é o correto.
        resultado.Value!.QuantidadeDe(ModalidadesFederaisLei12711.LiQ).Should().Be(1);
    }

    [Fact(DisplayName = "V3 — mín-1 ordenada eleva LB_Q 0→1; LI_Q permanece 0 (fora da ordem)")]
    public void MinimoUmaVaga_ElevaGrupoZerado_LiQForaDaOrdem()
    {
        Result<QuadroDeVagasDaLeiDeCotas> resultado = CalculadoraQuadroVagasLei12711.Calcular(
            40, 0.5m, Demografica(50, 0, 10), SemRetiradas, SemSuplementares);

        resultado.IsSuccess.Should().BeTrue();
        QuadroDeVagasDaLeiDeCotas quadro = resultado.Value!;
        quadro.QuantidadeDe(ModalidadesFederaisLei12711.LbQ).Should().Be(1);
        quadro.QuantidadeDe(ModalidadesFederaisLei12711.LiQ).Should().Be(0);
        quadro.ConferenciaDaReservaLegal.ReservaLegalEfetiva.Should().Be(21);
        quadro.QuantidadeDe(ModalidadesFederaisLei12711.Ac).Should().Be(19);
        quadro.ConferenciaDaReservaLegal.ExcedenteSobreAReservaLegal.Should().Be(1);
    }

    [Theory(DisplayName = "V4/INV-1 — PR fora de [0,5; 1] não é responsabilidade da calculadora (contrato do chamador)")]
    [InlineData(0.6, 24)]
    public void Pr_EscalaVrNominal(double pr, int vrEsperado)
    {
        Result<QuadroDeVagasDaLeiDeCotas> resultado = CalculadoraQuadroVagasLei12711.Calcular(
            40, (decimal)pr, Demografica(50, 10, 10), SemRetiradas, SemSuplementares);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.ConferenciaDaReservaLegal.ReservaLegalNominal.Should().Be(vrEsperado);
    }

    [Fact(DisplayName = "V5/CA-05 — resíduo nunca negativo: VO=50, PR=0,5, ppi=79, q=1,5, pcd=8,5")]
    public void ResiduoEp_NuncaNegativo()
    {
        Result<QuadroDeVagasDaLeiDeCotas> resultado = CalculadoraQuadroVagasLei12711.Calcular(
            50, 0.5m, Demografica(79, 1.5m, 8.5m), SemRetiradas, SemSuplementares);

        resultado.IsSuccess.Should().BeTrue();
        QuadroDeVagasDaLeiDeCotas quadro = resultado.Value!;
        quadro.QuantidadeDe(ModalidadesFederaisLei12711.LbEp).Should().BeGreaterThanOrEqualTo(0);
        quadro.QuantidadeDe(ModalidadesFederaisLei12711.LiEp).Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact(DisplayName = "V5/CA-06 — estouro é registrado, nunca silenciado: retirada de 2 em V")]
    public void Estouro_EhRegistrado_ComIndicadorDeCapagem()
    {
        Dictionary<string, int> retiradas = new(StringComparer.Ordinal) { ["V"] = 2 };

        Result<QuadroDeVagasDaLeiDeCotas> resultado = CalculadoraQuadroVagasLei12711.Calcular(
            50, 0.5m, Demografica(79, 1.5m, 8.5m), retiradas, SemSuplementares);

        resultado.IsSuccess.Should().BeTrue();
        QuadroDeVagasDaLeiDeCotas quadro = resultado.Value!;
        quadro.ConferenciaDaReservaLegal.ReservaLegalEfetiva.Should().Be(28);
        quadro.ConferenciaDaReservaLegal.ReservaLegalNominal.Should().Be(25);
        quadro.ConferenciaDaReservaLegal.ExcedenteSobreAReservaLegal.Should().Be(3);
        quadro.QuantidadeDe(ModalidadesFederaisLei12711.Ac).Should().Be(20);
        quadro.TotalDeVagasDoEdital.Should().Be(50);
    }

    [Fact(DisplayName = "V6/CA-07 — ampla concorrência negativa bloqueia: VO=10, PR=0,8, V retira 5")]
    public void AmplaConcorrenciaNegativa_Falha()
    {
        Dictionary<string, int> retiradas = new(StringComparer.Ordinal) { ["V"] = 5 };

        Result<QuadroDeVagasDaLeiDeCotas> resultado = CalculadoraQuadroVagasLei12711.Calcular(
            10, 0.8m, Demografica(50, 10, 10), retiradas, SemSuplementares);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.QuadroAmplaConcorrenciaNegativa");
    }

    [Fact(DisplayName = "CA-08 — conservação: Σ(sub-reservas) + Σ(retiradas) + AC = VO, em todo vetor calculado")]
    public void Quadro_ConservaVagasOfertadas()
    {
        Dictionary<string, int> retiradas = new(StringComparer.Ordinal) { ["V"] = 2 };
        int voBase = 50;

        Result<QuadroDeVagasDaLeiDeCotas> resultado = CalculadoraQuadroVagasLei12711.Calcular(
            voBase, 0.5m, Demografica(79, 1.5m, 8.5m), retiradas, SemSuplementares);

        resultado.IsSuccess.Should().BeTrue();
        QuadroDeVagasDaLeiDeCotas quadro = resultado.Value!;
        (quadro.ConferenciaDaReservaLegal.ReservaLegalEfetiva + retiradas.Values.Sum() + quadro.QuantidadeDe(ModalidadesFederaisLei12711.Ac)).Should().Be(voBase);
        quadro.TotalDeVagasDoEdital.Should().Be(voBase + SemSuplementares.Values.Sum());
    }

    [Fact(DisplayName = "V7/CA-09 — retirada com código de sub-reserva federal colide e é recusada")]
    public void RetiradaComCodigoDeSubReserva_Falha()
    {
        Dictionary<string, int> retiradas = new(StringComparer.Ordinal) { [ModalidadesFederaisLei12711.Ac] = 2 };

        Result<QuadroDeVagasDaLeiDeCotas> resultado = CalculadoraQuadroVagasLei12711.Calcular(
            40, 0.5m, Demografica(50, 10, 10), retiradas, SemSuplementares);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.QuadroChaveColide");
    }

    [Fact(DisplayName = "V8 — art. 11 §único: curso pequeno (VO=3) capa com prioridade LB, sem bloqueio")]
    public void CursoPequeno_PreenchePisoNaOrdemLegal_ECapaEmVo()
    {
        Result<QuadroDeVagasDaLeiDeCotas> resultado = CalculadoraQuadroVagasLei12711.Calcular(
            3, 0.5m, Demografica(79, 1.5m, 8.5m), SemRetiradas, SemSuplementares);

        resultado.IsSuccess.Should().BeTrue();
        QuadroDeVagasDaLeiDeCotas quadro = resultado.Value!;
        quadro.QuantidadeDe(ModalidadesFederaisLei12711.LbPpi).Should().Be(1);
        quadro.QuantidadeDe(ModalidadesFederaisLei12711.LbQ).Should().Be(1);
        quadro.QuantidadeDe(ModalidadesFederaisLei12711.LbPcd).Should().Be(1);
        quadro.QuantidadeDe(ModalidadesFederaisLei12711.LbEp).Should().Be(0);
        quadro.QuantidadeDe(ModalidadesFederaisLei12711.LiPpi).Should().Be(0);
        quadro.ConferenciaDaReservaLegal.VagasCortadasPorEscassez.Should().BePositive();
        quadro.ConferenciaDaReservaLegal.ReservaLegalEfetiva.Should().Be(3);
        quadro.QuantidadeDe(ModalidadesFederaisLei12711.Ac).Should().Be(0);
    }

    [Fact(DisplayName = "V9 — VO=2 capa só na ordem I,II (LB_PPI=1, LB_Q=1), resto 0")]
    public void VoMenorQueGruposGarantidos_CapaSoNaOrdemQueCabe()
    {
        Result<QuadroDeVagasDaLeiDeCotas> resultado = CalculadoraQuadroVagasLei12711.Calcular(
            2, 0.5m, Demografica(79, 1.5m, 8.5m), SemRetiradas, SemSuplementares);

        resultado.IsSuccess.Should().BeTrue();
        QuadroDeVagasDaLeiDeCotas quadro = resultado.Value!;
        quadro.QuantidadeDe(ModalidadesFederaisLei12711.LbPpi).Should().Be(1);
        quadro.QuantidadeDe(ModalidadesFederaisLei12711.LbQ).Should().Be(1);
        quadro.QuantidadeDe(ModalidadesFederaisLei12711.LbPcd).Should().Be(0);
        quadro.ConferenciaDaReservaLegal.VagasCortadasPorEscassez.Should().BePositive();
    }

    [Fact(DisplayName = "V10 — cap é condicional à escassez: VO=50 não é capado")]
    public void CapCondicionalAEscassez_CursoGrandeNaoCapa()
    {
        Result<QuadroDeVagasDaLeiDeCotas> resultado = CalculadoraQuadroVagasLei12711.Calcular(
            50, 0.5m, Demografica(79, 1.5m, 8.5m), SemRetiradas, SemSuplementares);

        resultado.IsSuccess.Should().BeTrue();
        QuadroDeVagasDaLeiDeCotas quadro = resultado.Value!;
        quadro.ConferenciaDaReservaLegal.VagasCortadasPorEscassez.Should().Be(0);
        quadro.ConferenciaDaReservaLegal.ReservaLegalEfetiva.Should().Be(28);
    }

    [Fact(DisplayName = "V11 — floor-first: piso de I,II,III vence o excedente de I")]
    public void FloorFirst_PisoDosGruposPosterioresVenceExcedenteDoAnterior_CasoSimetrico()
    {
        Result<QuadroDeVagasDaLeiDeCotas> resultado = CalculadoraQuadroVagasLei12711.Calcular(
            3, 1m, Demografica(100, 0, 0), SemRetiradas, SemSuplementares);

        resultado.IsSuccess.Should().BeTrue();
        QuadroDeVagasDaLeiDeCotas quadro = resultado.Value!;
        quadro.QuantidadeDe(ModalidadesFederaisLei12711.LbPpi).Should().Be(1, "o piso de LB_Q e LB_PCD vence o 2º de LB_PPI");
        quadro.QuantidadeDe(ModalidadesFederaisLei12711.LbQ).Should().Be(1);
        quadro.QuantidadeDe(ModalidadesFederaisLei12711.LbPcd).Should().Be(1);
        quadro.QuantidadeDe(ModalidadesFederaisLei12711.LiPpi).Should().Be(0);
        quadro.ConferenciaDaReservaLegal.ReservaLegalEfetiva.Should().Be(3);
        quadro.ConferenciaDaReservaLegal.VagasCortadasPorEscassez.Should().BePositive();
    }

    [Fact(DisplayName = "V12 — floor-first: mesmo achado, vetor irmão com percentuais assimétricos")]
    public void FloorFirst_PisoDosGruposPosterioresVenceExcedenteDoAnterior_CasoAssimetrico()
    {
        Result<QuadroDeVagasDaLeiDeCotas> resultado = CalculadoraQuadroVagasLei12711.Calcular(
            3, 1m, Demografica(79, 1.5m, 8.5m), SemRetiradas, SemSuplementares);

        resultado.IsSuccess.Should().BeTrue();
        QuadroDeVagasDaLeiDeCotas quadro = resultado.Value!;
        quadro.QuantidadeDe(ModalidadesFederaisLei12711.LbPpi).Should().Be(1);
        quadro.QuantidadeDe(ModalidadesFederaisLei12711.LbQ).Should().Be(1);
        quadro.QuantidadeDe(ModalidadesFederaisLei12711.LbPcd).Should().Be(1);
        quadro.QuantidadeDe(ModalidadesFederaisLei12711.LbEp).Should().Be(0);
        quadro.ConferenciaDaReservaLegal.ReservaLegalEfetiva.Should().Be(3);
        quadro.ConferenciaDaReservaLegal.VagasCortadasPorEscassez.Should().BePositive();
    }

    [Fact(DisplayName = "V13 — quantidade negativa de retirada é recusada")]
    public void QuantidadeNegativaDeRetirada_Falha()
    {
        Dictionary<string, int> retiradas = new(StringComparer.Ordinal) { ["V"] = -1 };

        Result<QuadroDeVagasDaLeiDeCotas> resultado = CalculadoraQuadroVagasLei12711.Calcular(
            40, 0.5m, Demografica(50, 10, 10), retiradas, SemSuplementares);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.QuantidadeVagaNegativa");
    }

    [Fact(DisplayName = "Suplemento aumenta o total publicado sem afetar VO nem a ampla concorrência")]
    public void Suplemento_AumentaTotalPublicado_SemAfetarAc()
    {
        Dictionary<string, int> suplementares = new(StringComparer.Ordinal) { ["CONV_MUNICIPAL"] = 3 };

        Result<QuadroDeVagasDaLeiDeCotas> resultado = CalculadoraQuadroVagasLei12711.Calcular(
            40, 0.5m, Demografica(50, 10, 10), SemRetiradas, suplementares);

        resultado.IsSuccess.Should().BeTrue();
        QuadroDeVagasDaLeiDeCotas quadro = resultado.Value!;
        quadro.QuantidadeDe(ModalidadesFederaisLei12711.Ac).Should().Be(20);
        quadro.QuantidadeDe("CONV_MUNICIPAL").Should().Be(3);
        quadro.TotalDeVagasDoEdital.Should().Be(43);
    }

    [Theory(DisplayName = "As vagas cortadas pela escassez são a demanda das sub-reservas que as vagas ofertadas não comportaram")]
    [InlineData(3, 1, 100, 0, 0, 5)]
    [InlineData(3, 0.5, 79, 1.5, 8.5, 4)]
    [InlineData(2, 0.5, 79, 1.5, 8.5, 5)]
    public void Escassez_InformaAsVagasCortadas(int vagasOfertadasBase, decimal percentual, decimal ppi, decimal quilombola, decimal pcd, int cortadas)
    {
        QuadroDeVagasDaLeiDeCotas quadro = CalculadoraQuadroVagasLei12711.Calcular(
            vagasOfertadasBase, percentual, Demografica(ppi, quilombola, pcd), SemRetiradas, SemSuplementares).Value!;

        quadro.ConferenciaDaReservaLegal.VagasCortadasPorEscassez.Should().Be(cortadas);
        quadro.ConferenciaDaReservaLegal.VagasCortadasPorEscassez.Should().BePositive();
        quadro.Vagas.Where(static v => v.ModalidadeCodigo != ModalidadesFederaisLei12711.Ac).Sum(static v => v.Quantidade)
            .Should().BeLessThanOrEqualTo(vagasOfertadasBase);
    }

    [Fact(DisplayName = "Sem escassez nenhuma vaga é cortada e o quadro não é considerado capado")]
    public void SemEscassez_NaoCortaVagas()
    {
        QuadroDeVagasDaLeiDeCotas quadro = CalculadoraQuadroVagasLei12711.Calcular(
            40, 0.5m, Demografica(50, 10, 10), SemRetiradas, SemSuplementares).Value!;

        quadro.ConferenciaDaReservaLegal.VagasCortadasPorEscassez.Should().Be(0);
        quadro.ConferenciaDaReservaLegal.VagasCortadasPorEscassez.Should().Be(0);
    }

    [Theory(DisplayName = "A fronteira da escassez: demanda igual às vagas ofertadas não corta; um a mais corta e a quilombola sem recorte de renda é servida por último")]
    [InlineData(10, 30, 20, 10, 0, 1)]
    [InlineData(8, 30, 20, 10, 1, 0)]
    [InlineData(10, 30, 50, 10, 3, 0)]
    public void FronteiraDaEscassez_CortaSoAQuePassaDasVagasOfertadas(
        int vagasOfertadasBase, decimal ppi, decimal quilombola, decimal pcd, int cortadas, int quilombolaSemRecorteDeRenda)
    {
        QuadroDeVagasDaLeiDeCotas quadro = CalculadoraQuadroVagasLei12711.Calcular(
            vagasOfertadasBase, 1m, Demografica(ppi, quilombola, pcd), SemRetiradas, SemSuplementares).Value!;

        quadro.ConferenciaDaReservaLegal.VagasCortadasPorEscassez.Should().Be(cortadas);
        quadro.QuantidadeDe(ModalidadesFederaisLei12711.LiQ).Should().Be(quilombolaSemRecorteDeRenda);
    }
}
