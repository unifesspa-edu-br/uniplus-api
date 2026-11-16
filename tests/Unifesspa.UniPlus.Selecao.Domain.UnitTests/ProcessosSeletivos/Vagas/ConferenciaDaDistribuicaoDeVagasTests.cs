namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Vagas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;

using static DistribuicaoDeVagasDeTeste;

/// <summary>
/// O que a distribuição de vagas declarada tem de cumprir antes de virar quadro: a forma, o teto das
/// vagas autorizadas, o rol fechado da regra, a coerência do ramo e a fronteira entre o que se calcula
/// e o que se declara. Todas as recusas se acumulam (ADR-0125), nessa ordem.
/// </summary>
public sealed class ConferenciaDaDistribuicaoDeVagasTests
{
    private static IReadOnlyList<FieldError> Conferir(
        DistribuicaoDeVagasDeclarada declarada, int? vagasAnuaisAutorizadas = null, string[]? modalidadesAdmitidas = null) =>
        ConferenciaDaDistribuicaoDeVagas.Conferir(declarada, vagasAnuaisAutorizadas, modalidadesAdmitidas);

    private static void DeveRecusarSo(IReadOnlyList<FieldError> recusas, string campo, string codigo) =>
        recusas.Should().ContainSingle().Which.Should().Match<FieldError>(
            recusa => recusa.Field == campo && recusa.Error.Code == codigo);

    [Fact(DisplayName = "A distribuição da Lei de Cotas completa passa sem recusa")]
    public void LeiDeCotasCompleta_SemRecusa() =>
        Conferir(DaLeiDeCotas()).Should().BeEmpty();

    [Fact(DisplayName = "O quadro fixado no edital, com quantidade em toda modalidade, passa sem recusa")]
    public void QuadroFixadoCompleto_SemRecusa() =>
        Conferir(FixadaNoEdital(Fixada("IND", 30), Fixada("QUI", 20))).Should().BeEmpty();

    [Theory(DisplayName = "As vagas ofertadas base de zero ou menos são recusadas")]
    [InlineData(0)]
    [InlineData(-5)]
    public void VagasOfertadasBase_NaoPositivas_Recusa(int base_) =>
        DeveRecusarSo(
            Conferir(DaLeiDeCotas() with { VagasOfertadasBase = base_ }),
            "vagasOfertadasBase", DistribuicaoDeVagasErrorCodes.VagasOfertadasBaseInvalido);

    [Theory(DisplayName = "O percentual da reserva legal fora de 0,5 a 1 é recusado no ramo da Lei de Cotas")]
    [InlineData(0.49)]
    [InlineData(1.01)]
    public void Percentual_ForaDoLimite_Recusa(double percentual) =>
        DeveRecusarSo(
            Conferir(DaLeiDeCotas() with { PercentualDaReservaLegal = (decimal)percentual }),
            "percentualDaReservaLegal", DistribuicaoDeVagasErrorCodes.PercentualDaReservaLegalForaDoLimite);

    [Theory(DisplayName = "O percentual da reserva legal nos limites é aceito")]
    [InlineData(0.5)]
    [InlineData(1)]
    public void Percentual_NosLimites_Aceita(double percentual) =>
        Conferir(DaLeiDeCotas() with { PercentualDaReservaLegal = (decimal)percentual }).Should().BeEmpty();

    [Fact(DisplayName = "O ramo da Lei de Cotas sem percentual da reserva legal é recusado")]
    public void LeiDeCotas_SemPercentual_Recusa() =>
        DeveRecusarSo(
            Conferir(DaLeiDeCotas() with { PercentualDaReservaLegal = null }),
            "percentualDaReservaLegal", DistribuicaoDeVagasErrorCodes.PercentualDaReservaLegalForaDoLimite);

    [Fact(DisplayName = "O quadro fixado no edital dispensa o percentual da reserva legal")]
    public void QuadroFixado_SemPercentual_Aceita() =>
        Conferir(FixadaNoEdital(Fixada("IND", 50)) with { PercentualDaReservaLegal = null }).Should().BeEmpty();

    [Fact(DisplayName = "A oferta sem modalidade é recusada")]
    public void SemModalidades_Recusa() =>
        DeveRecusarSo(
            Conferir(FixadaNoEdital()),
            "modalidades", DistribuicaoDeVagasErrorCodes.ModalidadesVazias);

    [Fact(DisplayName = "A modalidade selecionada duas vezes é recusada")]
    public void ModalidadeDuplicada_Recusa() =>
        DeveRecusarSo(
            Conferir(FixadaNoEdital(Fixada("IND", 25), Fixada("IND", 25))),
            "modalidades", DistribuicaoDeVagasErrorCodes.ModalidadeDuplicada);

    [Fact(DisplayName = "As vagas ofertadas base acima do teto autorizado no e-MEC são recusadas")]
    public void AcimaDoTeto_Recusa() =>
        DeveRecusarSo(
            Conferir(DaLeiDeCotas(), vagasAnuaisAutorizadas: 49),
            "vagasOfertadasBase", DistribuicaoDeVagasErrorCodes.VagasOfertadasBaseAcimaDasVagasAutorizadas);

    [Theory(DisplayName = "As vagas ofertadas base no teto, ou sem teto declarado, são aceitas")]
    [InlineData(50)]
    [InlineData(null)]
    public void NoTetoOuSemTeto_Aceita(int? teto) =>
        Conferir(DaLeiDeCotas(), teto).Should().BeEmpty();

    [Fact(DisplayName = "A modalidade fora do rol que a regra admite é recusada")]
    public void ForaDoRol_Recusa() =>
        DeveRecusarSo(
            Conferir(FixadaNoEdital(Fixada("IND", 25), Fixada("QUI", 25)), modalidadesAdmitidas: ["IND"]),
            "modalidades", DistribuicaoDeVagasErrorCodes.ModalidadeNaoAdmitidaPelaRegra);

    [Fact(DisplayName = "A modalidade do rol que falta na oferta é recusada")]
    public void RolIncompleto_Recusa() =>
        DeveRecusarSo(
            Conferir(FixadaNoEdital(Fixada("IND", 50)), modalidadesAdmitidas: ["IND", "QUI"]),
            "modalidades", DistribuicaoDeVagasErrorCodes.ModalidadeDoRolAusente);

    [Fact(DisplayName = "A regra sem rol declarado admite qualquer modalidade")]
    public void SemRol_AdmiteQualquer() =>
        Conferir(FixadaNoEdital(Fixada("IND", 50)), modalidadesAdmitidas: []).Should().BeEmpty();

    [Fact(DisplayName = "A Lei de Cotas sem a referência demográfica é recusada")]
    public void LeiDeCotas_SemReferenciaDemografica_Recusa() =>
        DeveRecusarSo(
            Conferir(DaLeiDeCotas() with { ReferenciaDemografica = null }),
            "referenciaDemografica", DistribuicaoDeVagasErrorCodes.ReferenciaDemograficaObrigatoria);

    [Fact(DisplayName = "A Lei de Cotas sem uma das oito federais ou sem a ampla concorrência é recusada")]
    public void LeiDeCotas_Incompleta_Recusa() =>
        DeveRecusarSo(
            Conferir(DaLeiDeCotas() with { Modalidades = [.. AsOitoFederaisMaisAmplaConcorrencia().Skip(1)] }),
            "modalidades", DistribuicaoDeVagasErrorCodes.ModalidadesFederaisIncompletas);

    [Fact(DisplayName = "A Lei de Cotas que retira de outra modalidade que não a ampla concorrência é recusada")]
    public void LeiDeCotas_RetiradaForaDaAmplaConcorrencia_Recusa()
    {
        List<ModalidadeSelecionada> modalidades = AsOitoFederaisMaisAmplaConcorrencia();
        modalidades.Add(Modalidade(
            "AC_PCD", NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.RetiradaDeOutraModalidade, 2, ModalidadesFederaisLei12711.LbPpi));

        DeveRecusarSo(
            Conferir(DaLeiDeCotas() with { Modalidades = modalidades }),
            "modalidades", DistribuicaoDeVagasErrorCodes.RetiradaFederalDeveSerDeAmplaConcorrencia);
    }

    [Fact(DisplayName = "A Lei de Cotas sem a regra de ajuste é recusada")]
    public void LeiDeCotas_SemRegraDeAjuste_Recusa() =>
        DeveRecusarSo(
            Conferir(DaLeiDeCotas() with { RegraDeAjuste = null }),
            "regraDeAjuste", DistribuicaoDeVagasErrorCodes.RegraAjusteObrigatoria);

    [Fact(DisplayName = "A Lei de Cotas com motor de redução declarado é recusada")]
    public void LeiDeCotas_ComMotorDeReducao_Recusa() =>
        DeveRecusarSo(
            Conferir(DaLeiDeCotas() with { AjusteDoQuadro = new Selecao.Domain.ValueObjects.ArgsReduzirDe("AC") }),
            "ajusteDoQuadro", DistribuicaoDeVagasErrorCodes.MotorDeAjusteVedadoNaLei12711);

    [Fact(DisplayName = "O quadro fixado no edital com referência demográfica é recusado")]
    public void QuadroFixado_ComReferenciaDemografica_Recusa() =>
        DeveRecusarSo(
            Conferir(FixadaNoEdital(Fixada("IND", 50)) with { ReferenciaDemografica = Demografica() }),
            "referenciaDemografica", DistribuicaoDeVagasErrorCodes.ReferenciaDemograficaIndevida);

    [Fact(DisplayName = "A modalidade calculada da Lei de Cotas com quantidade fixada é recusada")]
    public void LeiDeCotas_CalculadaComQuantidade_Recusa()
    {
        List<ModalidadeSelecionada> modalidades = AsOitoFederaisMaisAmplaConcorrencia();
        modalidades[0] = Modalidade(
            ModalidadesFederaisLei12711.LbPpi, NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDaReservaLegal, 3);

        DeveRecusarSo(
            Conferir(DaLeiDeCotas() with { Modalidades = modalidades }),
            "modalidades", DistribuicaoDeVagasErrorCodes.QuantidadeCalculadaNaoInformavel);
    }

    [Fact(DisplayName = "A retirada da Lei de Cotas sem quantidade fixada é recusada")]
    public void LeiDeCotas_RetiradaSemQuantidade_Recusa()
    {
        List<ModalidadeSelecionada> modalidades = AsOitoFederaisMaisAmplaConcorrencia();
        modalidades.Add(Modalidade(
            "AC_PCD", NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.RetiradaDeOutraModalidade, null, ModalidadesFederaisLei12711.Ac));

        DeveRecusarSo(
            Conferir(DaLeiDeCotas() with { Modalidades = modalidades }),
            "modalidades", DistribuicaoDeVagasErrorCodes.QuantidadeDeclaradaObrigatoria);
    }

    [Fact(DisplayName = "No quadro fixado no edital, a modalidade sem quantidade é recusada")]
    public void QuadroFixado_SemQuantidade_Recusa() =>
        DeveRecusarSo(
            Conferir(FixadaNoEdital(
                Fixada("IND", 50),
                Modalidade("QUI", NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.SuplementarAoTotal))),
            "modalidades", DistribuicaoDeVagasErrorCodes.QuadroModalidadeAusente);

    private static DistribuicaoDeVagasDeclarada ComUmaRecusaDeCadaConferencia() => DaLeiDeCotas() with
    {
        VagasOfertadasBase = 60,
        PercentualDaReservaLegal = 2m,
        ReferenciaDemografica = null,
        Modalidades = [.. AsOitoFederaisMaisAmplaConcorrencia().Select(static m => m.Codigo == ModalidadesFederaisLei12711.Ac
            ? Modalidade(m.Codigo, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas, 5)
            : m)],
    };

    [Fact(DisplayName = "As recusas se acumulam, uma por causa, na ordem das conferências")]
    public void Recusas_Acumulam_NaOrdemDasConferencias()
    {
        IReadOnlyList<FieldError> recusas = Conferir(
            ComUmaRecusaDeCadaConferencia(), vagasAnuaisAutorizadas: 50, modalidadesAdmitidas: [ModalidadesFederaisLei12711.Ac]);

        recusas.Select(static r => r.Error.Code).Should().Equal(
            DistribuicaoDeVagasErrorCodes.PercentualDaReservaLegalForaDoLimite,
            DistribuicaoDeVagasErrorCodes.VagasOfertadasBaseAcimaDasVagasAutorizadas,
            DistribuicaoDeVagasErrorCodes.ModalidadeNaoAdmitidaPelaRegra,
            DistribuicaoDeVagasErrorCodes.ReferenciaDemograficaObrigatoria,
            DistribuicaoDeVagasErrorCodes.QuantidadeCalculadaNaoInformavel);
    }

    [Fact(DisplayName = "As recusas de forma se acumulam, na ordem das regras de forma")]
    public void RecusasDeForma_Acumulam()
    {
        IReadOnlyList<FieldError> recusas = Conferir(FixadaNoEdital() with { VagasOfertadasBase = 0, PercentualDaReservaLegal = 2m });

        recusas.Select(static r => r.Error.Code).Should().Equal(
            DistribuicaoDeVagasErrorCodes.VagasOfertadasBaseInvalido,
            DistribuicaoDeVagasErrorCodes.PercentualDaReservaLegalForaDoLimite,
            DistribuicaoDeVagasErrorCodes.ModalidadesVazias);
    }

    [Fact(DisplayName = "As recusas de coerência da Lei de Cotas se acumulam, na ordem das regras")]
    public void RecusasDeCoerenciaDaLeiDeCotas_Acumulam()
    {
        DistribuicaoDeVagasDeclarada declarada = DaLeiDeCotas() with
        {
            ReferenciaDemografica = null,
            RegraDeAjuste = null,
            AjusteDoQuadro = new Selecao.Domain.ValueObjects.ArgsReduzirDe("AC"),
            Modalidades = [.. AsOitoFederaisMaisAmplaConcorrencia().Skip(1)],
        };

        Conferir(declarada).Select(static r => r.Error.Code).Should().Equal(
            DistribuicaoDeVagasErrorCodes.ReferenciaDemograficaObrigatoria,
            DistribuicaoDeVagasErrorCodes.RegraAjusteObrigatoria,
            DistribuicaoDeVagasErrorCodes.MotorDeAjusteVedadoNaLei12711,
            DistribuicaoDeVagasErrorCodes.ModalidadesFederaisIncompletas);
    }

    [Fact(DisplayName = "As recusas de fronteira se acumulam, uma por modalidade, na ordem das modalidades")]
    public void RecusasDeFronteira_Acumulam()
    {
        IReadOnlyList<FieldError> recusas = Conferir(FixadaNoEdital(
            Modalidade("IND", NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.SuplementarAoTotal),
            Fixada("QUI", 50),
            Modalidade("PCD", NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.SuplementarAoTotal)));

        recusas.Should().HaveCount(2);
        recusas[0].Error.Message.Should().Contain("IND");
        recusas[1].Error.Message.Should().Contain("PCD");
    }

    [Fact(DisplayName = "O código de regra fora do vocabulário não recebe conferência de ramo nem de fronteira")]
    public void RegraDesconhecida_SoConferenciaDeForma() =>
        Conferir(ComRegraDeOutroCodigo(FixadaNoEdital(Modalidade("X", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.SuplementarAoTotal))))
            .Should().BeEmpty();

    private static DistribuicaoDeVagasDeclarada ComRegraDeOutroCodigo(DistribuicaoDeVagasDeclarada declarada) =>
        declarada with { RegraDeDistribuicao = Regra("DISTRIB-OUTRA") };
}
