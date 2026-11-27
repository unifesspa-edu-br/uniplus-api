namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Vagas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;

/// <summary>
/// O quadro de vagas de uma oferta tem a forma do ramo que o produziu: o da Lei de Cotas leva a
/// conferência da reserva legal e não leva ajuste; o fixado no edital leva as vagas retiradas no
/// ajuste e não leva reserva legal. Nenhum dos dois carrega o campo do outro.
/// </summary>
public sealed class QuadroDeVagasTests
{
    private static readonly VagasDaModalidade[] Quadro =
    [
        new("AC", 12),
        new("LB_PPI", 8),
    ];

    [Fact(DisplayName = "A reserva legal efetiva acima da nominal deixa excedente igual à diferença")]
    public void Efetiva_AcimaDaNominal_ExcedenteEIgualADiferenca()
    {
        ConferenciaDaReservaLegal conferencia = new(ReservaLegalNominal: 10, ReservaLegalEfetiva: 13, VagasCortadasPorEscassez: 0);

        conferencia.ExcedenteSobreAReservaLegal.Should().Be(3);
    }

    [Theory(DisplayName = "A reserva legal efetiva igual ou abaixo da nominal não deixa excedente")]
    [InlineData(10, 10)]
    [InlineData(10, 7)]
    [InlineData(0, 0)]
    public void Efetiva_NaoAcimaDaNominal_SemExcedente(int nominal, int efetiva)
    {
        ConferenciaDaReservaLegal conferencia = new(nominal, efetiva, VagasCortadasPorEscassez: 0);

        conferencia.ExcedenteSobreAReservaLegal.Should().Be(0);
    }

    [Fact(DisplayName = "O quadro da Lei de Cotas guarda as vagas, o total do edital e a conferência da reserva legal")]
    public void QuadroDaLeiDeCotas_GuardaOQueFoiCalculado()
    {
        ConferenciaDaReservaLegal conferencia = new(10, 10, VagasCortadasPorEscassez: 2);

        QuadroDeVagasDaLeiDeCotas quadro = new(Quadro, TotalDeVagasDoEdital: 20, conferencia);

        quadro.Vagas.Should().Equal(Quadro);
        quadro.TotalDeVagasDoEdital.Should().Be(20);
        quadro.ConferenciaDaReservaLegal.VagasCortadasPorEscassez.Should().Be(2);
    }

    [Fact(DisplayName = "O quadro fixado no edital soma as vagas retiradas no ajuste")]
    public void QuadroFixado_SomaAsVagasRetiradasNoAjuste()
    {
        VagasDaModalidade[] retiradas = [new("AC", 3), new("LB_PPI", 2)];

        QuadroDeVagasFixadoNoEdital quadro = new(Quadro, TotalDeVagasDoEdital: 20, retiradas);

        quadro.TotalDeVagasRetiradasNoAjuste.Should().Be(5);
    }

    [Fact(DisplayName = "O quadro fixado sem ajuste não retirou vaga nenhuma")]
    public void QuadroFixado_SemAjuste_NaoRetirouVaga()
    {
        QuadroDeVagasFixadoNoEdital quadro = new(Quadro, TotalDeVagasDoEdital: 20, VagasRetiradasNoAjuste: []);

        quadro.TotalDeVagasRetiradasNoAjuste.Should().Be(0);
    }

    [Fact(DisplayName = "A quantidade de uma modalidade do quadro vem pelo código, e a ausente vale zero")]
    public void QuantidadeDe_PeloCodigo()
    {
        QuadroDeVagas quadro = new QuadroDeVagasFixadoNoEdital(Quadro, 20, []);

        quadro.QuantidadeDe("LB_PPI").Should().Be(8);
        quadro.QuantidadeDe("LI_Q").Should().Be(0);
    }

    private static readonly VagasDaModalidade[] ForaDeOrdem = [new("ampla", 1), new("L2", 2), new("L10", 3), new("AC", 4), new("L1", 5)];

    [Fact(DisplayName = "As vagas pelo código da modalidade saem em ordem ordinal, sem tratar os números dentro do código")]
    public void VagasPeloCodigoDaModalidade_FicamEmOrdemOrdinal()
    {
        QuadroDeVagasFixadoNoEdital quadro = new(ForaDeOrdem, TotalDeVagasDoEdital: 15, []);

        quadro.VagasPeloCodigoDaModalidade().Select(static vagas => vagas.ModalidadeCodigo).Should().Equal("AC", "L1", "L10", "L2", "ampla");
        quadro.Vagas.Select(static vagas => vagas.ModalidadeCodigo).Should().Equal("ampla", "L2", "L10", "AC", "L1");
    }

    [Fact(DisplayName = "As vagas retiradas no ajuste saem em ordem ordinal do código da modalidade")]
    public void VagasRetiradasPeloCodigoDaModalidade_FicamEmOrdemOrdinal()
    {
        QuadroDeVagasFixadoNoEdital quadro = new(Quadro, TotalDeVagasDoEdital: 20, ForaDeOrdem);

        quadro.VagasRetiradasPeloCodigoDaModalidade().Select(static vagas => vagas.ModalidadeCodigo).Should().Equal("AC", "L1", "L10", "L2", "ampla");
    }

    [Fact(DisplayName = "O quadro da Lei de Cotas também entrega as vagas em ordem ordinal do código")]
    public void QuadroDaLeiDeCotas_VagasPeloCodigoDaModalidade_FicamEmOrdemOrdinal()
    {
        QuadroDeVagasDaLeiDeCotas quadro = new(ForaDeOrdem, TotalDeVagasDoEdital: 15, new(10, 10, VagasCortadasPorEscassez: 0));

        quadro.VagasPeloCodigoDaModalidade().Select(static vagas => vagas.ModalidadeCodigo).Should().Equal("AC", "L1", "L10", "L2", "ampla");
    }
}
