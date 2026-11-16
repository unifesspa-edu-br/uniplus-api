namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaConfiguracaoDeVagas;

public sealed class ConfiguracaoDistribuicaoVagasRamoDoQuadroTests
{
    private static ConfiguracaoDistribuicaoVagas CriarLeiDeCotas()
    {
        List<ModalidadeSelecionada> modalidades = AsOitoFederaisMaisAc();

        return ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 50, PercentualDaReservaLegal = 0.5m, RegraDeDistribuicao = RegraLei12711(), RegraDeAjuste = RegraAjuste(), ReferenciaDemografica = Demografica(), Modalidades = modalidades },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;
    }

    [Fact(DisplayName = "Lei de Cotas guarda o percentual declarado e a conferência da reserva legal, sem campo do ramo fixado")]
    public void Criar_LeiDeCotas_GuardaPercentualEConferenciaDaReservaLegal()
    {
        ConfiguracaoDistribuicaoVagas configuracao = CriarLeiDeCotas();

        configuracao.PercentualDaReservaLegal.Should().Be(0.5m);
        configuracao.ReservaLegalNominal.Should().NotBeNull();
        configuracao.ReservaLegalEfetiva.Should().NotBeNull();
        configuracao.ExcedenteSobreReservaLegal.Should().NotBeNull();
        configuracao.VagasCortadasPorEscassez.Should().NotBeNull();
        configuracao.AjusteDoQuadro.Should().BeNull();
        configuracao.VagasRetiradasNoAjuste.Should().BeNull();
    }

    [Fact(DisplayName = "O quadro da Lei de Cotas expõe a conferência da reserva legal e o total do edital guardado")]
    public void Quadro_LeiDeCotas_ExpoeConferenciaETotalGuardado()
    {
        ConfiguracaoDistribuicaoVagas configuracao = CriarLeiDeCotas();

        QuadroDeVagasDaLeiDeCotas quadro = configuracao.Quadro.Should().BeOfType<QuadroDeVagasDaLeiDeCotas>().Subject;

        quadro.TotalDeVagasDoEdital.Should().Be(configuracao.TotalDeVagasDoEdital);
        quadro.ConferenciaDaReservaLegal.ReservaLegalNominal.Should().Be(configuracao.ReservaLegalNominal);
        quadro.ConferenciaDaReservaLegal.ReservaLegalEfetiva.Should().Be(configuracao.ReservaLegalEfetiva);
        quadro.ConferenciaDaReservaLegal.VagasCortadasPorEscassez.Should().Be(configuracao.VagasCortadasPorEscassez);
        quadro.Vagas.Should().BeEquivalentTo(configuracao.VagasOfertadas.Select(v => new VagasDaModalidade(v.ModalidadeCodigo, v.Quantidade)));
    }

    [Fact(DisplayName = "Quadro fixado no edital não guarda percentual nem conferência da reserva legal, e guarda o ajuste e as retiradas")]
    public void Criar_FixadoNoEdital_GuardaAjusteERetiradasSemCampoDaReservaLegal()
    {
        ConfiguracaoDistribuicaoVagas configuracao = CriarFixadoNoEditalComAjuste();

        configuracao.PercentualDaReservaLegal.Should().BeNull("o percentual só existe no ramo da Lei de Cotas");
        configuracao.ReservaLegalNominal.Should().BeNull();
        configuracao.ReservaLegalEfetiva.Should().BeNull();
        configuracao.ExcedenteSobreReservaLegal.Should().BeNull();
        configuracao.VagasCortadasPorEscassez.Should().BeNull();
        configuracao.AjusteDoQuadro.Should().Be(new ArgsReduzirDe("AC"));
        configuracao.VagasRetiradasNoAjuste.Should().BeEquivalentTo([new VagasDaModalidade("AC", 4)]);
    }

    [Fact(DisplayName = "O quadro fixado no edital expõe as retiradas do ajuste e o total guardado")]
    public void Quadro_FixadoNoEdital_ExpoeRetiradasETotalGuardado()
    {
        ConfiguracaoDistribuicaoVagas configuracao = CriarFixadoNoEditalComAjuste();

        QuadroDeVagasFixadoNoEdital quadro = configuracao.Quadro.Should().BeOfType<QuadroDeVagasFixadoNoEdital>().Subject;

        quadro.TotalDeVagasDoEdital.Should().Be(40);
        quadro.TotalDeVagasRetiradasNoAjuste.Should().Be(4);
    }
}
