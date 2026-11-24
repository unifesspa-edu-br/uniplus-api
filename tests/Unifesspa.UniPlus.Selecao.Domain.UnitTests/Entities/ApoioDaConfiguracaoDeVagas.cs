namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>Regras, modalidades e referência demográfica de teste da configuração de distribuição de vagas.</summary>
internal static class ApoioDaConfiguracaoDeVagas
{
    internal static ReferenciaRegra RegraLei12711() =>
        ReferenciaRegra.De(RegraDeDistribuicaoDeVagas.Lei12711.Codigo(), "v1", new string('a', 64));

    internal static ReferenciaRegra RegraInstitucional() =>
        ReferenciaRegra.De(RegraDeDistribuicaoDeVagas.Institucional.Codigo(), "v1", new string('b', 64));

    internal static ReferenciaRegra RegraPsiq() =>
        ReferenciaRegra.De(RegraDeDistribuicaoDeVagas.PorAcrescimoIndigenaEQuilombola.Codigo(), "v1", new string('d', 64));

    internal static ReferenciaRegra RegraComAcPcd() =>
        ReferenciaRegra.De(RegraDeDistribuicaoDeVagas.ComAmplaConcorrenciaEPessoaComDeficiencia.Codigo(), "v1", new string('e', 64));

    internal static ReferenciaRegra RegraLei12711ComAcPcd() =>
        ReferenciaRegra.De(RegraDeDistribuicaoDeVagas.Lei12711ComAmplaConcorrenciaEPessoaComDeficiencia.Codigo(), "v1", new string('f', 64));

    internal static readonly string[] RolLei12711 =
        ["AC", "LB_PPI", "LB_Q", "LB_PCD", "LB_EP", "LI_PPI", "LI_Q", "LI_PCD", "LI_EP"];

    internal static readonly string[] RolLei12711ComAcPcd = [.. RolLei12711, "AC_PCD"];

    internal static ReferenciaRegra RegraAjuste() =>
        ReferenciaRegra.De("AJUSTE_VAGAS_ARTIGO_11_PARAGRAFO_UNICO", "v1", new string('c', 64));

    internal static ReferenciaReservaDemograficaSnapshot Demografica() =>
        ReferenciaReservaDemograficaSnapshot.Criar(Guid.CreateVersion7(), "2022", 79m, 1.5m, 8.5m, "Censo 2022").Value!;

    internal static ModalidadeSelecionada Modalidade(
        string codigo,
        NaturezaLegalModalidade natureza,
        ComposicaoVagasModalidade composicao,
        int? quantidadeDeclarada = null,
        string? composicaoOrigemCodigo = null) =>
        ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da(codigo, natureza, composicao) with { ComposicaoOrigemCodigo = composicaoOrigemCodigo, RegraRemanejamento = natureza == NaturezaLegalModalidade.CotaReservada ? RegraRemanejamentoModalidade.SegueOrdemDeRemanejamento : null, BaseLegal = "base legal" }, quantidadeDeclarada).Value!;

    internal static List<ModalidadeSelecionada> AsOitoFederaisMaisAc() =>
    [
        Modalidade(ModalidadesFederaisLei12711.LbPpi, NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDaReservaLegal),
        Modalidade(ModalidadesFederaisLei12711.LbQ, NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDaReservaLegal),
        Modalidade(ModalidadesFederaisLei12711.LbPcd, NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDaReservaLegal),
        Modalidade(ModalidadesFederaisLei12711.LbEp, NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDaReservaLegal),
        Modalidade(ModalidadesFederaisLei12711.LiPpi, NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDaReservaLegal),
        Modalidade(ModalidadesFederaisLei12711.LiQ, NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDaReservaLegal),
        Modalidade(ModalidadesFederaisLei12711.LiPcd, NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDaReservaLegal),
        Modalidade(ModalidadesFederaisLei12711.LiEp, NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDaReservaLegal),
        Modalidade(ModalidadesFederaisLei12711.Ac, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas),
    ];

    /// <summary>Cria a configuração com o percentual integral da reserva legal e o cadastro vivo da oferta de Medicina.</summary>
    internal static Result<ConfiguracaoDistribuicaoVagas> CriarConfiguracao(
        int vagasOfertadasBase,
        ReferenciaRegra regra,
        IReadOnlyList<ModalidadeSelecionada> modalidades,
        IReadOnlyCollection<string>? modalidadesAdmitidas = null) =>
        ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = vagasOfertadasBase, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = regra, Modalidades = modalidades },
            new FatosDaOfertaNoCadastro { ModalidadesAdmitidas = modalidadesAdmitidas, OfertaCurso = OfertaCursoDeTeste.Medicina });

    /// <summary>Quadro fixado no edital que excede em 4 vagas e é reconciliado reduzindo a ampla concorrência.</summary>
    internal static ConfiguracaoDistribuicaoVagas CriarFixadoNoEditalComAjuste()
    {
        List<ModalidadeSelecionada> modalidades =
        [
            Modalidade("AC", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas, quantidadeDeclarada: 40),
            Modalidade("AC_PCD", NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.RetiradaDeOutraModalidade, quantidadeDeclarada: 4, composicaoOrigemCodigo: "AC"),
        ];

        return ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 40, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = RegraComAcPcd(), RegraDeAjuste = RegraAjuste(), AjusteDoQuadro = new ParametrosReduzirDe("AC"), Modalidades = modalidades },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;
    }
}
