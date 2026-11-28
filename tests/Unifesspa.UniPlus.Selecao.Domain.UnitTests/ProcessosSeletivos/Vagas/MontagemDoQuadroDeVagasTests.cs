namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Vagas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static DistribuicaoDeVagasDeTeste;

/// <summary>
/// A declaração conferida vira o quadro com a forma do ramo: o da Lei de Cotas pelo cálculo do art. 10,
/// com a conferência da reserva legal; o fixado no edital pela soma declarada, reduzida pelo ajuste, com
/// as vagas retiradas. A mesma declaração reproduz o quadro que a entidade monta hoje.
/// </summary>
public sealed class MontagemDoQuadroDeVagasTests
{
    private static ModalidadeSelecionada Retirada(string codigo, int quantidade) =>
        Modalidade(codigo, NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.RetiradaDeOutraModalidade, quantidade, ModalidadesFederaisLei12711.Ac);

    private static DistribuicaoDeVagasDeclarada DaLeiDeCotasCom(int vagasOfertadasBase, decimal percentual, params ModalidadeSelecionada[] acrescimos) =>
        DaLeiDeCotas() with
        {
            VagasOfertadasBase = vagasOfertadasBase,
            PercentualDaReservaLegal = percentual,
            Modalidades = [.. AsOitoFederaisMaisAmplaConcorrencia(), .. acrescimos],
        };

    private static QuadroDeVagas Montar(DistribuicaoDeVagasDeclarada declarada) =>
        MontagemDoQuadroDeVagas.Montar(declarada).Value!;

    private static ConfiguracaoDistribuicaoVagas DaEntidade(DistribuicaoDeVagasDeclarada declarada) =>
        ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = declarada.VagasOfertadasBase, PercentualDaReservaLegal = declarada.PercentualDaReservaLegal ?? 1m, RegraDeDistribuicao = declarada.RegraDeDistribuicao, RegraDeAjuste = declarada.RegraDeAjuste, ReferenciaDemografica = declarada.ReferenciaDemografica, AjusteDoQuadro = declarada.AjusteDoQuadro, Modalidades = declarada.Modalidades },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;

    private static (string Codigo, int Quantidade)[] DoQuadro(QuadroDeVagas quadro) =>
        [.. quadro.Vagas.Select(static v => (v.ModalidadeCodigo, v.Quantidade))];

    private static (string Codigo, int Quantidade)[] DaEntidade(ConfiguracaoDistribuicaoVagas entidade) =>
        [.. entidade.VagasOfertadas.Select(static v => (v.ModalidadeCodigo, v.Quantidade))];

    [Fact(DisplayName = "A Lei de Cotas monta o quadro, o total do edital e a conferência da reserva legal")]
    public void LeiDeCotas_MontaQuadroEConferencia()
    {
        QuadroDeVagasDaLeiDeCotas quadro = Montar(DaLeiDeCotasCom(50, 0.5m)).Should().BeOfType<QuadroDeVagasDaLeiDeCotas>().Subject;

        quadro.Vagas.Sum(static v => v.Quantidade).Should().Be(50);
        quadro.TotalDeVagasDoEdital.Should().Be(50);
        quadro.ConferenciaDaReservaLegal.ReservaLegalNominal.Should().Be(25);
        quadro.ConferenciaDaReservaLegal.VagasCortadasPorEscassez.Should().Be(0);
    }

    [Fact(DisplayName = "A Lei de Cotas sem vagas que cheguem para as reservas informa as vagas cortadas pela escassez")]
    public void LeiDeCotas_ComEscassez_InformaAsVagasCortadas()
    {
        QuadroDeVagasDaLeiDeCotas quadro = (QuadroDeVagasDaLeiDeCotas)Montar(DaLeiDeCotasCom(3, 1m));

        quadro.ConferenciaDaReservaLegal.VagasCortadasPorEscassez.Should().BePositive();
    }

    [Fact(DisplayName = "A retirada e o suplemento declarados entram no quadro e o suplemento soma ao total do edital")]
    public void LeiDeCotas_RetiradaESuplemento_EntramNoQuadro()
    {
        ModalidadeSelecionada suplementar = Fixada("IND", 4);

        QuadroDeVagas quadro = Montar(DaLeiDeCotasCom(50, 0.5m, Retirada("AC_PCD", 2), suplementar));

        quadro.QuantidadeDe("AC_PCD").Should().Be(2);
        quadro.QuantidadeDe("IND").Should().Be(4);
        quadro.TotalDeVagasDoEdital.Should().Be(54);
    }

    [Fact(DisplayName = "A Lei de Cotas com a retirada maior que a ampla concorrência é recusada")]
    public void LeiDeCotas_AmplaNegativa_Recusa()
    {
        Result<QuadroDeVagas> resultado = MontagemDoQuadroDeVagas.Montar(DaLeiDeCotasCom(10, 1m, Retirada("AC_PCD", 40)));

        resultado.IsFailure.Should().BeTrue();
    }

    [Fact(DisplayName = "A modalidade calculada que o art. 10 não conhece é recusada como ausente do quadro")]
    public void LeiDeCotas_CalculadaForaDoArt10_RecusaComoAusente()
    {
        DistribuicaoDeVagasDeclarada declarada = DaLeiDeCotasCom(
            50, 0.5m, Modalidade("OUTRA", NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.DentroDaReservaLegal));

        DomainError erro = MontagemDoQuadroDeVagas.Montar(declarada).Error!;

        erro.Code.Should().Be(DistribuicaoDeVagasErrorCodes.QuadroModalidadeAusente);
        erro.Message.Should().Be("A modalidade OUTRA — Modalidade OUTRA está selecionada mas não aparece no quadro calculado.");
    }

    [Fact(DisplayName = "O quadro fixado com a soma igual às vagas ofertadas base não retira vaga")]
    public void Fixado_SomaIgual_SemRetirada()
    {
        QuadroDeVagasFixadoNoEdital quadro = (QuadroDeVagasFixadoNoEdital)Montar(FixadaNoEdital(Fixada("IND", 30), Fixada("QUI", 20)));

        DoQuadro(quadro).Should().Equal(("IND", 30), ("QUI", 20));
        quadro.TotalDeVagasDoEdital.Should().Be(50);
        quadro.VagasRetiradasNoAjuste.Should().BeEmpty();
    }

    [Fact(DisplayName = "O quadro fixado com excesso reduzido de uma modalidade registra as vagas retiradas dela")]
    public void Fixado_ReduzirDe_RegistraAsRetiradas()
    {
        DistribuicaoDeVagasDeclarada declarada = FixadaNoEdital(Fixada("IND", 40), Fixada("QUI", 20)) with { AjusteDoQuadro = new ParametrosReduzirDe("IND") };

        QuadroDeVagasFixadoNoEdital quadro = (QuadroDeVagasFixadoNoEdital)Montar(declarada);

        DoQuadro(quadro).Should().Equal(("IND", 30), ("QUI", 20));
        quadro.VagasRetiradasNoAjuste.Should().Equal(new VagasDaModalidade("IND", 10));
        quadro.TotalDeVagasDoEdital.Should().Be(50);
    }

    [Fact(DisplayName = "O quadro fixado com excesso repartido entre modalidades registra a retirada de cada uma")]
    public void Fixado_ReduzirProporcionalEm_RegistraAsRetiradas()
    {
        DistribuicaoDeVagasDeclarada declarada = FixadaNoEdital(Fixada("IND", 30), Fixada("QUI", 30)) with { AjusteDoQuadro = new ParametrosReduzirProporcionalEm(["IND", "QUI"]) };

        QuadroDeVagasFixadoNoEdital quadro = (QuadroDeVagasFixadoNoEdital)Montar(declarada);

        quadro.Vagas.Sum(static v => v.Quantidade).Should().Be(50);
        quadro.TotalDeVagasRetiradasNoAjuste.Should().Be(10);
    }

    [Fact(DisplayName = "O quadro fixado reduzido por maior resto não depende da ordem em que as modalidades foram declaradas")]
    public void Fixado_ReduzirProporcionalEm_ComRestosIguais_IndependeDaOrdemDeDeclaracao()
    {
        ParametrosRegraAjusteDistribuicao ajuste = new ParametrosReduzirProporcionalEm(["AC", "IND", "QUI"]);
        DistribuicaoDeVagasDeclarada emOrdemAlfabetica = FixadaNoEdital(Fixada("AC", 20), Fixada("IND", 20), Fixada("QUI", 20)) with { AjusteDoQuadro = ajuste };
        DistribuicaoDeVagasDeclarada emOutraOrdem = FixadaNoEdital(Fixada("QUI", 20), Fixada("AC", 20), Fixada("IND", 20)) with { AjusteDoQuadro = ajuste };

        (string Codigo, int Quantidade)[] alfabetica = DoQuadro(Montar(emOrdemAlfabetica));
        (string Codigo, int Quantidade)[] outra = DoQuadro(Montar(emOutraOrdem));

        outra.OrderBy(static v => v.Codigo, StringComparer.Ordinal).Should().Equal(alfabetica);
        outra.Select(static v => v.Codigo).Should().Equal("AC", "IND", "QUI");
    }

    [Fact(DisplayName = "O quadro fixado que não completa as vagas ofertadas base é recusado")]
    public void Fixado_SomaMenor_Recusa() =>
        MontagemDoQuadroDeVagas.Montar(FixadaNoEdital(Fixada("IND", 20))).Error!.Code
            .Should().Be(DistribuicaoDeVagasErrorCodes.QuadroNaoCompletaAsVagasOfertadasBase);

    [Fact(DisplayName = "O quadro fixado que passa das vagas ofertadas base sem ajuste declarado é recusado")]
    public void Fixado_SomaMaiorSemAjuste_Recusa() =>
        MontagemDoQuadroDeVagas.Montar(FixadaNoEdital(Fixada("IND", 60))).Error!.Code
            .Should().Be(DistribuicaoDeVagasErrorCodes.QuadroExcedeAsVagasOfertadasBase);

    [Fact(DisplayName = "O código de regra fora da partição fechada lança exceção")]
    public void RegraForaDaParticao_LancaExcecao()
    {
        DistribuicaoDeVagasDeclarada declarada = FixadaNoEdital(Fixada("IND", 50)) with { RegraDeDistribuicao = Regra("DISTRIB-OUTRA") };

        Action montar = () => MontagemDoQuadroDeVagas.Montar(declarada);

        montar.Should().Throw<ArgumentException>().WithMessage("*DISTRIB-OUTRA*");
    }

    [Fact(DisplayName = "A declaração da Lei de Cotas reproduz o quadro que a entidade monta")]
    public void LeiDeCotas_ReproduzOQuadroDaEntidade()
    {
        DistribuicaoDeVagasDeclarada declarada = DaLeiDeCotasCom(37, 0.5m, Retirada("AC_PCD", 2), Fixada("IND", 4));
        ConfiguracaoDistribuicaoVagas entidade = DaEntidade(declarada);

        QuadroDeVagasDaLeiDeCotas quadro = (QuadroDeVagasDaLeiDeCotas)Montar(declarada);

        DoQuadro(quadro).Should().Equal(DaEntidade(entidade));
        quadro.TotalDeVagasDoEdital.Should().Be(entidade.TotalDeVagasDoEdital);
        quadro.ConferenciaDaReservaLegal.Should().Be(((QuadroDeVagasDaLeiDeCotas)entidade.Quadro).ConferenciaDaReservaLegal);
    }

    [Fact(DisplayName = "A declaração fixada com ajuste reproduz o quadro que a entidade monta")]
    public void Fixado_ReproduzOQuadroDaEntidade()
    {
        DistribuicaoDeVagasDeclarada declarada = FixadaNoEdital(Fixada("IND", 40), Fixada("QUI", 25)) with { AjusteDoQuadro = new ParametrosReduzirDe("IND") };
        ConfiguracaoDistribuicaoVagas entidade = DaEntidade(declarada);

        QuadroDeVagasFixadoNoEdital quadro = (QuadroDeVagasFixadoNoEdital)Montar(declarada);

        DoQuadro(quadro).Should().Equal(DaEntidade(entidade));
        quadro.TotalDeVagasDoEdital.Should().Be(entidade.TotalDeVagasDoEdital);
        quadro.TotalDeVagasRetiradasNoAjuste.Should().Be(((QuadroDeVagasFixadoNoEdital)entidade.Quadro).TotalDeVagasRetiradasNoAjuste);
    }
}
