namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Vagas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;

using static DistribuicaoDeVagasDeTeste;

/// <summary>
/// Uma regra da distribuição declarada só deixa de rodar quando um dado dela foi recusado na resolução do
/// cadastro: a recusa de um dado não se desdobra em recusas falsas sobre o que dele depende.
/// </summary>
public sealed class ConferenciaDaDistribuicaoDeVagasComDadoRecusadoTests
{
    private static IReadOnlyList<FieldError> Conferir(
        DistribuicaoDeVagasDeclarada declarada, string[]? modalidadesAdmitidas = null) =>
        ConferenciaDaDistribuicaoDeVagas.Conferir(declarada, null, modalidadesAdmitidas);

    private static string[] Codigos(IReadOnlyList<FieldError> recusas) =>
        [.. recusas.Select(static r => r.Error.Code)];

    private static DistribuicaoDeVagasDeclarada ComRegraRecusada(DistribuicaoDeVagasDeclarada declarada) => declarada with
    {
        RegraDeDistribuicao = null,
        PercentualDaReservaLegal = null,
        ReferenciaDemografica = null,
        RegraDeAjuste = null,
        Recusados = Recusando(DadoDaDistribuicaoDeVagas.RegraDeDistribuicao),
    };

    [Fact(DisplayName = "A regra de distribuição recusada não gera recusa de percentual, referência demográfica, regra de ajuste nem modalidades federais")]
    public void RegraRecusada_NaoGeraRecusasDoRamo() =>
        Conferir(
            ComRegraRecusada(DaLeiDeCotas() with { Modalidades = [.. AsOitoFederaisMaisAmplaConcorrencia().Skip(1)] }),
            modalidadesAdmitidas: [ModalidadesFederaisLei12711.Ac])
            .Should().BeEmpty();

    [Fact(DisplayName = "A regra de distribuição recusada não impede a recusa da modalidade repetida")]
    public void RegraRecusada_MantemModalidadeRepetida() =>
        Codigos(Conferir(ComRegraRecusada(FixadaNoEdital(Fixada("IND", 25), Fixada("IND", 25)))))
            .Should().Equal(DistribuicaoDeVagasErrorCodes.ModalidadeDuplicada);

    [Fact(DisplayName = "A regra de distribuição recusada não confere a fronteira da quantidade nem a regra de ajuste do quadro")]
    public void RegraRecusada_NaoConfereFronteiraNemAjusteDoQuadro()
    {
        ModalidadeSelecionada semQuantidade = Modalidade(
            "IND", NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.SuplementarAoTotal);
        DistribuicaoDeVagasDeclarada declarada = FixadaNoEdital(semQuantidade) with
        {
            AjusteDoQuadro = new Selecao.Domain.ValueObjects.ParametrosReduzirDe("AC"),
        };

        Conferir(ComRegraRecusada(declarada)).Should().BeEmpty();
    }

    [Fact(DisplayName = "A referência demográfica recusada não gera a recusa de referência obrigatória")]
    public void ReferenciaRecusada_NaoGeraReferenciaObrigatoria() =>
        Conferir(DaLeiDeCotas() with
        {
            ReferenciaDemografica = null,
            Recusados = Recusando(DadoDaDistribuicaoDeVagas.ReferenciaDemografica),
        }).Should().BeEmpty();

    [Fact(DisplayName = "A referência demográfica recusada não impede a recusa da regra de ajuste ausente")]
    public void ReferenciaRecusada_MantemRegraDeAjusteObrigatoria() =>
        Codigos(Conferir(DaLeiDeCotas() with
        {
            ReferenciaDemografica = null,
            RegraDeAjuste = null,
            Recusados = Recusando(DadoDaDistribuicaoDeVagas.ReferenciaDemografica),
        })).Should().Equal(DistribuicaoDeVagasErrorCodes.RegraAjusteObrigatoria);

    [Fact(DisplayName = "A regra de ajuste recusada não gera a recusa de regra de ajuste obrigatória")]
    public void RegraDeAjusteRecusada_NaoGeraRegraDeAjusteObrigatoria() =>
        Conferir(DaLeiDeCotas() with
        {
            RegraDeAjuste = null,
            Recusados = Recusando(DadoDaDistribuicaoDeVagas.RegraDeAjuste),
        }).Should().BeEmpty();

    [Fact(DisplayName = "A modalidade recusada não impede a recusa da aceita fora do rol da regra")]
    public void ModalidadeRecusada_MantemAAceitaForaDoRol() =>
        Codigos(Conferir(
            FixadaNoEdital(Fixada("IND", 25)) with { Recusados = Recusando(DadoDaDistribuicaoDeVagas.Modalidades) },
            modalidadesAdmitidas: ["QUI"]))
            .Should().Equal(DistribuicaoDeVagasErrorCodes.ModalidadeNaoAdmitidaPelaRegra);

    [Fact(DisplayName = "A modalidade recusada suprime a recusa de modalidade do rol ausente")]
    public void ModalidadeRecusada_SuprimeAModalidadeDoRolAusente() =>
        Conferir(
            FixadaNoEdital(Fixada("IND", 25)) with { Recusados = Recusando(DadoDaDistribuicaoDeVagas.Modalidades) },
            modalidadesAdmitidas: ["IND", "QUI"])
            .Should().BeEmpty();

    [Fact(DisplayName = "A modalidade recusada suprime as modalidades federais incompletas")]
    public void ModalidadeRecusada_SuprimeModalidadesFederaisIncompletas() =>
        Conferir(DaLeiDeCotas() with
        {
            Modalidades = [.. AsOitoFederaisMaisAmplaConcorrencia().Skip(1)],
            Recusados = Recusando(DadoDaDistribuicaoDeVagas.Modalidades),
        }).Should().BeEmpty();

    [Fact(DisplayName = "A modalidade recusada mantém a fronteira da quantidade das modalidades aceitas")]
    public void ModalidadeRecusada_MantemFronteiraDasAceitas()
    {
        ModalidadeSelecionada semQuantidade = Modalidade(
            "IND", NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.SuplementarAoTotal);
        DistribuicaoDeVagasDeclarada declarada = FixadaNoEdital(semQuantidade) with
        {
            Recusados = Recusando(DadoDaDistribuicaoDeVagas.Modalidades),
        };

        Codigos(Conferir(declarada)).Should().Equal(DistribuicaoDeVagasErrorCodes.QuadroModalidadeAusente);
    }

    [Fact(DisplayName = "No quadro fixado, a modalidade cuja linha veio sem quantidade não gera também a recusa de quadro com modalidade ausente")]
    public void QuadroFixado_QuantidadeRecusadaNaLinha_NaoGeraQuadroModalidadeAusente()
    {
        ModalidadeSelecionada semQuantidade = Modalidade(
            "IND", NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.SuplementarAoTotal);
        DistribuicaoDeVagasDeclarada declarada = FixadaNoEdital(semQuantidade) with
        {
            ModalidadesComQuantidadeRecusada = new HashSet<Guid> { semQuantidade.ModalidadeOrigemId },
        };

        Conferir(declarada).Should().BeEmpty();
    }

    [Fact(DisplayName = "Na Lei de Cotas, a modalidade de retirada cuja linha veio sem quantidade não gera também a recusa de quantidade obrigatória")]
    public void LeiDeCotas_QuantidadeRecusadaNaRetirada_NaoGeraQuantidadeObrigatoria()
    {
        List<ModalidadeSelecionada> modalidades = AsOitoFederaisMaisAmplaConcorrencia();
        ModalidadeSelecionada retirada = Modalidade(
            "AC_PCD", NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.RetiradaDeOutraModalidade, quantidadeDeclarada: null, ModalidadesFederaisLei12711.Ac);
        modalidades.Add(retirada);
        DistribuicaoDeVagasDeclarada declarada = DaLeiDeCotas() with
        {
            Modalidades = modalidades,
            ModalidadesComQuantidadeRecusada = new HashSet<Guid> { retirada.ModalidadeOrigemId },
        };

        Codigos(Conferir(declarada)).Should().NotContain(DistribuicaoDeVagasErrorCodes.QuantidadeDeclaradaObrigatoria);
    }

    [Fact(DisplayName = "A linha do quadro sem quantidade é recusada no campo dela e a com zero é aceita")]
    public void QuadroDoPedido_QuantidadeAusente_RecusaNoCampoDaLinha()
    {
        IReadOnlyList<FieldError> recusas = ConferenciaDaDistribuicaoDeVagas.ConferirFormaDoPedido(60, 0.5m, 1, [0, null, 5, null]);

        recusas.Select(static r => (r.Field, r.Error.Code)).Should().Equal(
            ("quadro[1].quantidade", CodigosDeValidacao.Obrigatorio), ("quadro[3].quantidade", CodigosDeValidacao.Obrigatorio));
    }

    [Fact(DisplayName = "A quantidade que falta na linha anterior sai antes da negativa da linha seguinte")]
    public void QuadroDoPedido_AusenteAntesDaNegativa_RecusaNaOrdemDasLinhas()
    {
        IReadOnlyList<FieldError> recusas = ConferenciaDaDistribuicaoDeVagas.ConferirFormaDoPedido(60, 0.5m, 1, [null, -1]);

        recusas.Select(static r => (r.Field, r.Error.Code)).Should().Equal(
            ("quadro[0].quantidade", CodigosDeValidacao.Obrigatorio),
            ("quadro[1].quantidade", DistribuicaoDeVagasErrorCodes.QuantidadeVagaNegativa));
    }

    [Fact(DisplayName = "A quantidade negativa na linha anterior sai antes da que falta na linha seguinte")]
    public void QuadroDoPedido_NegativaAntesDaAusente_RecusaNaOrdemDasLinhas()
    {
        IReadOnlyList<FieldError> recusas = ConferenciaDaDistribuicaoDeVagas.ConferirFormaDoPedido(60, 0.5m, 1, [-1, null]);

        recusas.Select(static r => (r.Field, r.Error.Code)).Should().Equal(
            ("quadro[0].quantidade", DistribuicaoDeVagasErrorCodes.QuantidadeVagaNegativa),
            ("quadro[1].quantidade", CodigosDeValidacao.Obrigatorio));
    }

    [Fact(DisplayName = "A declaração sem regra de distribuição não determina o ramo")]
    public void DeclaracaoSemRegra_NaoDeterminaORamo() =>
        (FixadaNoEdital(Fixada("IND", 50)) with { RegraDeDistribuicao = null }).RamoDeterminado.Should().BeFalse();

    [Fact(DisplayName = "A regra de distribuição recusada não determina o ramo, mesmo com a regra preenchida")]
    public void RegraRecusada_NaoDeterminaORamo() =>
        (FixadaNoEdital(Fixada("IND", 50)) with { Recusados = Recusando(DadoDaDistribuicaoDeVagas.RegraDeDistribuicao) })
            .RamoDeterminado.Should().BeFalse();
}
