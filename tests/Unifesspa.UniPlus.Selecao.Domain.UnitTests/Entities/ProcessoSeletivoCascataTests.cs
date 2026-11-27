namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cascata;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Cobertura de <c>CoberturaDaCascata.CausasDaCascata</c> e do item
/// "Cascata de remanejamento" em <see cref="ProcessoSeletivo.AvaliarConformidade"/>
/// (cascata de remanejamento de cotas, UNI-REQ-0056) — a validação cross-dimensão que varre
/// <c>DistribuicaoVagas</c> por oferta, fora do agregador genérico de
/// conformidade (<see cref="ProcessoSeletivo.PendenciaDeConformidade"/>).
/// </summary>
public sealed class ProcessoSeletivoCascataTests
{
    private static readonly string HashFixo = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];

    private static ReferenciaRegra RegraLei12711() =>
        ReferenciaRegra.De(RegraDeDistribuicaoDeVagas.Lei12711.Codigo(), "v1", HashFixo);

    private static ReferenciaRegra RegraInstitucional() =>
        ReferenciaRegra.De(RegraDeDistribuicaoDeVagas.Institucional.Codigo(), "v1", HashFixo);

    private static ReferenciaRegra RegraLei12711ComAcPcd() =>
        ReferenciaRegra.De(RegraDeDistribuicaoDeVagas.Lei12711ComAmplaConcorrenciaEPessoaComDeficiencia.Codigo(), "v1", HashFixo);

    private static ReferenciaRegra RegraAjuste() =>
        ReferenciaRegra.De("AJUSTE_VAGAS_ARTIGO_11_PARAGRAFO_UNICO", "v1", HashFixo);

    private static ReferenciaReservaDemograficaSnapshot Demografica() =>
        ReferenciaReservaDemograficaSnapshot.Criar(Guid.CreateVersion7(), "2022", 79m, 1.5m, 8.5m, "Censo 2022").Value!;

    private static ReferenciaRegra RegraCascata() =>
        ReferenciaRegra.De(CriterioDeRemanejamento.OrdemDaLei12711.Codigo(), "v1", HashFixo);

    private static ModalidadeSelecionada Modalidade(
        string codigo, NaturezaLegalModalidade natureza, ComposicaoVagasModalidade composicao, int? quantidadeDeclarada = null) =>
        ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da(codigo, natureza, composicao) with { RegraRemanejamento = natureza == NaturezaLegalModalidade.CotaReservada ? RegraRemanejamentoModalidade.SegueOrdemDeRemanejamento : null, BaseLegal = "base legal" }, quantidadeDeclarada).Value!;

    private static List<ModalidadeSelecionada> AsOitoFederaisMaisAc() =>
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

    private static ConfiguracaoDistribuicaoVagas OfertaFederalCompleta() =>
        ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 50, PercentualDaReservaLegal = 0.5m, RegraDeDistribuicao = RegraLei12711(), RegraDeAjuste = RegraAjuste(), ReferenciaDemografica = Demografica(), Modalidades = AsOitoFederaisMaisAc() },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;

    private static DestinoRemanejamento Destino(string origem, int ordem, string destino) =>
        DestinoRemanejamento.Criar(origem, ordem, destino).Value!;

    /// <summary>A matriz legal completa (8×7), fallback AC — a mesma semeada em REMANEJAMENTO_ORDEM_LEI_12711 v1.</summary>
    private static ConfiguracaoCascataRemanejamento CascataCompleta() => ConfiguracaoCascataRemanejamento.Criar(
        RegraCascata(), ModalidadesFederaisLei12711.Ac, [.. OrigensDaCascataFederal.SelectMany(FilaLegalDaOrigem)]).Value!;

    private static readonly string[] OrigensDaCascataFederal =
    [
        ModalidadesFederaisLei12711.LbPpi, ModalidadesFederaisLei12711.LbQ, ModalidadesFederaisLei12711.LbPcd, ModalidadesFederaisLei12711.LbEp,
        ModalidadesFederaisLei12711.LiPpi, ModalidadesFederaisLei12711.LiQ, ModalidadesFederaisLei12711.LiPcd, ModalidadesFederaisLei12711.LiEp,
    ];

    private static IEnumerable<DestinoRemanejamento> FilaLegalDaOrigem(string origem) =>
        OrigensDaCascataFederal.Where(o => o != origem).Select((destino, i) => Destino(origem, i + 1, destino));

    /// <summary>
    /// A matriz legal completa, com TODOS os sete destinos de <paramref name="origemAlvo"/>
    /// substituídos por códigos fora da oferta — as outras sete origens continuam com a fila
    /// legal completa e resolvível. Substituir só UM destino não bastaria: os outros seis da
    /// mesma origem continuariam resolvíveis (a conferência usa <c>Any</c> sobre a fila inteira da
    /// origem), então a origem-alvo precisa ficar SEM nenhum destino resolvível na oferta.
    /// </summary>
    private static ConfiguracaoCascataRemanejamento CascataComOrigemSemDestinoResolvivel(string origemAlvo) =>
        ConfiguracaoCascataRemanejamento.Criar(
            RegraCascata(), ModalidadesFederaisLei12711.Ac,
            [.. OrigensDaCascataFederal.SelectMany(origem => origem == origemAlvo ? DestinosForaDaOferta(origem) : FilaLegalDaOrigem(origem))]).Value!;

    private static IEnumerable<DestinoRemanejamento> DestinosForaDaOferta(string origem) =>
        Enumerable.Range(1, 7).Select(i => Destino(origem, i, $"FORA_DA_OFERTA_{i}"));

    private static ConfiguracaoCascataRemanejamento CascataComFallbackEDestinoForaDaOferta() =>
        ConfiguracaoCascataRemanejamento.Criar(
            RegraCascata(), "FALLBACK_INEXISTENTE",
            [
                Destino(ModalidadesFederaisLei12711.LbPpi, 1, ModalidadesFederaisLei12711.LbQ),
                Destino(ModalidadesFederaisLei12711.LbPpi, 2, "DESTINO_QUE_NAO_EXISTE"),
            ]).Value!;

    private static DomainError? PrimeiraCausa(ProcessoSeletivo processo) =>
        CoberturaDaCascata.CausasDaCascata(processo.Declaracao()) is { Count: > 0 } causas ? causas[0].Error : null;

    private static ProcessoSeletivo NovoProcessoComOferta(ConfiguracaoDistribuicaoVagas oferta)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        processo.DefinirDistribuicaoVagas([oferta], FatosDeModalidadeDeTeste.DoCatalogo).IsSuccess.Should().BeTrue();
        return processo;
    }

    [Fact(DisplayName = "CausasDaCascata com oferta federal completa e cascata legal completa é null (Ok)")]
    public void CausasDaCascata_OfertaFederalComCascataCompleta_Ok()
    {
        ProcessoSeletivo processo = NovoProcessoComOferta(OfertaFederalCompleta());
        processo.DefinirCascataRemanejamento(CascataCompleta()).IsSuccess.Should().BeTrue();

        CoberturaDaCascata.CausasDaCascata(processo.Declaracao()).Should().BeEmpty();
    }

    [Fact(DisplayName = "CausasDaCascata com oferta sob a variação -COM-AC-PCD e cascata legal completa é null (Ok)")]
    public void CausasDaCascata_OfertaFederalComAcPcdComCascataCompleta_Ok()
    {
        ModalidadeSelecionada acPcd = ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da("AC_PCD", NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.RetiradaDeOutraModalidade) with { ComposicaoOrigemCodigo = ModalidadesFederaisLei12711.Ac, BaseLegal = "base legal" }, 2).Value!;
        ConfiguracaoDistribuicaoVagas oferta = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 50, PercentualDaReservaLegal = 0.5m, RegraDeDistribuicao = RegraLei12711ComAcPcd(), RegraDeAjuste = RegraAjuste(), ReferenciaDemografica = Demografica(), Modalidades = [.. AsOitoFederaisMaisAc(), acPcd] },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;
        ProcessoSeletivo processo = NovoProcessoComOferta(oferta);
        processo.DefinirCascataRemanejamento(CascataCompleta()).IsSuccess.Should().BeTrue();

        CoberturaDaCascata.CausasDaCascata(processo.Declaracao()).Should().BeEmpty(
            "sem EhRegraFederal cobrindo a variação, OfertaForaDoRegimeFederal recusaria com CascataForaDoRegimeFederal");
    }

    [Fact(DisplayName = "CausasDaCascata com oferta federal e SEM cascata configurada recusa com CascataOrigemAusente, nomeando oferta e modalidade")]
    public void CausasDaCascata_OfertaFederalSemCascata_RecusaComOrigemAusente()
    {
        ConfiguracaoDistribuicaoVagas oferta = OfertaFederalCompleta();
        ProcessoSeletivo processo = NovoProcessoComOferta(oferta);

        DomainError? pendencia = PrimeiraCausa(processo);

        pendencia.Should().NotBeNull();
        pendencia!.Code.Should().Be("ProcessoSeletivo.CascataOrigemAusente");
        pendencia.Message.Should().Contain(oferta.OfertaCursoOrigemId.ToString());
    }

    [Fact(DisplayName = "CausasDaCascata com SegueCascata fora do regime federal (institucional) recusa com CascataForaDoRegimeFederal")]
    public void CausasDaCascata_ForaDoRegimeFederal_Recusa()
    {
        ModalidadeSelecionada cotaFederalSobRegimeErrado = Modalidade(
            "LB_PPI", NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.SuplementarAoTotal, quantidadeDeclarada: 2);
        ModalidadeSelecionada ampla = Modalidade("AC", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas, quantidadeDeclarada: 8);
        ConfiguracaoDistribuicaoVagas ofertaInstitucional = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 10, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = RegraInstitucional(), Modalidades = [cotaFederalSobRegimeErrado, ampla] },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;
        ProcessoSeletivo processo = NovoProcessoComOferta(ofertaInstitucional);

        DomainError? pendencia = PrimeiraCausa(processo);

        pendencia.Should().NotBeNull();
        pendencia!.Code.Should().Be("ProcessoSeletivo.CascataForaDoRegimeFederal");
    }

    [Fact(DisplayName = "CausasDaCascata com fallback da cascata fora das modalidades da oferta recusa com CascataFallbackNaoSelecionadoNaOferta")]
    public void CausasDaCascata_FallbackForaDaOferta_Recusa()
    {
        ProcessoSeletivo processo = NovoProcessoComOferta(OfertaFederalCompleta());
        ConfiguracaoCascataRemanejamento cascataComFallbackInexistente = ConfiguracaoCascataRemanejamento.Criar(
            RegraCascata(), "FALLBACK_INEXISTENTE",
            [Destino(ModalidadesFederaisLei12711.LbPpi, 1, ModalidadesFederaisLei12711.LbQ)]).Value!;
        processo.DefinirCascataRemanejamento(cascataComFallbackInexistente).IsSuccess.Should().BeTrue();

        DomainError? pendencia = PrimeiraCausa(processo);

        pendencia.Should().NotBeNull();
        pendencia!.Code.Should().Be("ProcessoSeletivo.CascataFallbackNaoSelecionadoNaOferta");
    }

    [Fact(DisplayName = "CausasDaCascata acumula o fallback fora da oferta, as origens sem destino e o destino desconhecido, nessa ordem")]
    public void CausasDaCascata_DefeitosIndependentes_AcumulamNaOrdem()
    {
        ProcessoSeletivo processo = NovoProcessoComOferta(OfertaFederalCompleta());
        processo.DefinirCascataRemanejamento(CascataComFallbackEDestinoForaDaOferta()).IsSuccess.Should().BeTrue();

        string[] codigos = [.. CoberturaDaCascata.CausasDaCascata(processo.Declaracao()).Select(static causa => causa.Error.Code)];

        codigos.Should().Equal(
            [
                "ProcessoSeletivo.CascataFallbackNaoSelecionadoNaOferta",
                .. Enumerable.Repeat("ProcessoSeletivo.CascataOrigemAusente", 7),
                "ProcessoSeletivo.CascataDestinoDesconhecido",
            ],
            "o fallback é da oferta, as sete origens sem destino vêm depois, e o destino desconhecido é conferido por último, sobre a cascata");
    }

    [Fact(DisplayName = "CausasDaCascata com destino da origem fora das modalidades da oferta recusa com CascataFallbackNaoSelecionadoNaOferta")]
    public void CausasDaCascata_DestinoDaOrigemNaoResolvivelNaOferta_Recusa()
    {
        // As outras sete origens mantêm a fila legal completa (todas resolvíveis nesta
        // oferta) — só o 1º destino de LB_PPI é trocado por um código fora da oferta,
        // isolando a asserção a essa única origem.
        ProcessoSeletivo processo = NovoProcessoComOferta(OfertaFederalCompleta());
        ConfiguracaoCascataRemanejamento cascataComDestinoForaDaOferta = CascataComOrigemSemDestinoResolvivel(ModalidadesFederaisLei12711.LbPpi);
        processo.DefinirCascataRemanejamento(cascataComDestinoForaDaOferta).IsSuccess.Should().BeTrue();

        DomainError? pendencia = PrimeiraCausa(processo);

        pendencia.Should().NotBeNull();
        pendencia!.Code.Should().Be("ProcessoSeletivo.CascataFallbackNaoSelecionadoNaOferta");
    }

    private static ConfiguracaoDistribuicaoVagas OfertaSoDeAmpla() => ConfiguracaoDistribuicaoVagas.Criar(
        Guid.CreateVersion7(),
        new DistribuicaoDeVagasDeclarada
        {
            VagasOfertadasBase = 10,
            PercentualDaReservaLegal = 1m,
            RegraDeDistribuicao = RegraInstitucional(),
            Modalidades = [Modalidade(ModalidadesFederaisLei12711.Ac, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas, quantidadeDeclarada: 10)],
        },
        new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;

    private static ConfiguracaoCascataRemanejamento CascataComUmDestinoQueNenhumaOfertaTem()
    {
        List<DestinoRemanejamento> destinos = [.. OrigensDaCascataFederal.SelectMany(DestinosDaOrigemComUmDesconhecido)];

        destinos.Should().HaveCount(56);
        Result<ConfiguracaoCascataRemanejamento> resultadoCascata = ConfiguracaoCascataRemanejamento.Criar(
            RegraCascata(), ModalidadesFederaisLei12711.Ac, destinos);
        resultadoCascata.IsSuccess.Should().BeTrue(resultadoCascata.Error?.Message);

        return resultadoCascata.Value!;
    }

    // LB_Q perde um destino legal para abrir espaço sob o teto de 56 destinos; LB_PPI ganha um destino que nenhuma oferta tem.
    private static IEnumerable<DestinoRemanejamento> DestinosDaOrigemComUmDesconhecido(string origem) =>
        origem == ModalidadesFederaisLei12711.LbQ ? FilaLegalDaOrigem(origem).SkipLast(1)
        : origem == ModalidadesFederaisLei12711.LbPpi ? FilaLegalDaOrigem(origem).Append(Destino(origem, OrigensDaCascataFederal.Length, "DESTINO_QUE_NAO_EXISTE"))
        : FilaLegalDaOrigem(origem);

    [Fact(DisplayName = "CausasDaCascata com origem que nenhuma oferta marca como SegueCascata recusa com CascataOrigemNaoSegueCascata")]
    public void CausasDaCascata_OrigemNaoSegueCascataEmNenhumaOferta_Recusa()
    {
        // Só AC (ampla) na oferta — nenhuma modalidade SegueCascata, então o loop por
        // oferta não recusa nada; a checagem global (origens declaradas na cascata vs.
        // origens SegueCascata em alguma oferta) é quem detecta a origem morta.
        ProcessoSeletivo processo = NovoProcessoComOferta(OfertaSoDeAmpla());
        ConfiguracaoCascataRemanejamento cascataComOrigemMorta = ConfiguracaoCascataRemanejamento.Criar(
            RegraCascata(), ModalidadesFederaisLei12711.Ac,
            [Destino(ModalidadesFederaisLei12711.LbPpi, 1, ModalidadesFederaisLei12711.Ac)]).Value!;
        processo.DefinirCascataRemanejamento(cascataComOrigemMorta).IsSuccess.Should().BeTrue();

        DomainError? pendencia = PrimeiraCausa(processo);

        pendencia.Should().NotBeNull();
        pendencia!.Code.Should().Be("ProcessoSeletivo.CascataOrigemNaoSegueCascata");
    }

    [Fact(DisplayName = "CausasDaCascata com destino que não é modalidade de nenhuma oferta recusa com CascataDestinoDesconhecido")]
    public void CausasDaCascata_DestinoDesconhecidoEmTodoOProcesso_Recusa()
    {
        // LB_PPI mantém os 7 destinos legais (todos resolvíveis nesta oferta — o
        // per-oferta loop não recusa) e ganha um 8º destino apontando para um código
        // que não é modalidade de NENHUMA oferta do processo — só a checagem GLOBAL
        // (que roda depois de todas as ofertas passarem) alcança esse caso. LB_Q perde
        // um destino legal (fica com 6, ainda contíguo a partir de 1) para abrir espaço
        // sob o teto de 56 destinos (7×6 + 6 + 8 = 56) sem comprometer a resolubilidade
        // de LB_Q nesta oferta (os 6 restantes continuam todos selecionados).
        ProcessoSeletivo processo = NovoProcessoComOferta(OfertaFederalCompleta());
        processo.DefinirCascataRemanejamento(CascataComUmDestinoQueNenhumaOfertaTem()).IsSuccess.Should().BeTrue();

        DomainError? pendencia = PrimeiraCausa(processo);

        pendencia.Should().NotBeNull();
        pendencia!.Code.Should().Be("ProcessoSeletivo.CascataDestinoDesconhecido");
    }

    [Fact(DisplayName = "AvaliarConformidade com processo só de ampla concorrência (sem SegueCascata) tem o item Ok, sem cascata")]
    public void AvaliarConformidade_SemModalidadeSegueCascata_ItemCascataOk()
    {
        ModalidadeSelecionada ampla = Modalidade(ModalidadesFederaisLei12711.Ac, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas, quantidadeDeclarada: 10);
        ConfiguracaoDistribuicaoVagas oferta = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 10, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = RegraInstitucional(), Modalidades = [ampla] },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;
        ProcessoSeletivo processo = NovoProcessoComOferta(oferta);

        CoberturaDaCascata.CausasDaCascata(processo.Declaracao()).Should().BeEmpty();
        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo)).Should().Contain(item => item.Codigo == "cascata_pendente" && item.Ok);
    }

    [Fact(DisplayName = "AvaliarConformidade com oferta federal sem cascata tem o item Cascata de remanejamento não-Ok")]
    public void AvaliarConformidade_OfertaFederalSemCascata_ItemCascataNaoOk()
    {
        ProcessoSeletivo processo = NovoProcessoComOferta(OfertaFederalCompleta());

        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo)).Should().Contain(item => item.Codigo == "cascata_pendente" && !item.Ok);
    }

    private static FaseCronograma FaseDeResultadoFinal() => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        OrigensDoTipoDeFaseDeTeste.Da(
            origemId: Guid.CreateVersion7(), codigo: "RESULTADO_FINAL", origemData: OrigemDataFase.NoEdital, agrupaEtapas: true, permiteComplementacao: false, coletaInscricao: true,
            coletaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(
            Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [ProdutoDaFase.Criar("RESULTADO_FINAL", PapelDaPublicacaoPrevista.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    private static void DefinirEtapaAtendimentoEDistribuicaoFederal(ProcessoSeletivo processo)
    {
        processo.DefinirEtapas(
            [EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
                new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: "RESULTADO_FINAL"),
                new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!]).IsSuccess.Should().BeTrue();
        processo.DefinirAtendimentoEspecializado(
            AtendimentoEspecializado.Criar([], [], []).Value!).IsSuccess.Should().BeTrue();
        processo.DefinirDistribuicaoVagas([OfertaFederalCompleta()], FatosDeModalidadeDeTeste.DoCatalogo).IsSuccess.Should().BeTrue();
    }

    private static void DefinirClassificacaoETaxaDeclaradas(ProcessoSeletivo processo)
    {
        processo.DefinirClassificacao(
            ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
            {
                RegraCalculo = ReferenciaRegra.De(RegraDeCalculoDaNota.ClassificacaoImportada.Codigo(), "v1", HashFixo),
                RegraOrdemAlocacao = ReferenciaRegra.De(RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria.Codigo(), "v1", HashFixo),
            }).Value!).IsSuccess.Should().BeTrue();
        // Item estrutural — declarado para manter os demais itens Ok, já que
        // este teste isola a ausência de cascata como a ÚNICA pendência.
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!).IsSuccess.Should().BeTrue();
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);
    }

    // Processo estruturalmente conforme (etapas, atendimento, classificação,
    // cronograma) — mas com oferta federal SEM cascata configurada. Antes da
    // separação dos checklists, o item "Cascata de remanejamento" entrava na
    // lista que o agregador genérico varre, e essa ausência derrubava
    // PendenciaDeConformidade() com ConformidadeInsuficiente — mascarando o erro
    // nomeado que PendenciaDaCascata() (e CA-04) prometem.
    [Fact(DisplayName = "PendenciaDeConformidade (agregador genérico) fica Ok mesmo com a cascata pendente — só PendenciaDaCascata recusa")]
    public void PendenciaDeConformidade_IgnoraAusenciaDaCascata()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        processo.DefinirCronogramaFases(
            [FaseDeResultadoFinal()],
            []).IsSuccess.Should().BeTrue();
        DefinirEtapaAtendimentoEDistribuicaoFederal(processo);
        DefinirClassificacaoETaxaDeclaradas(processo);

        CausasDeConformidadeDeTeste.De(processo).Should().BeEmpty("os itens estruturais estão todos completos — a cascata não é um deles");
        CoberturaDaCascata.CausasDaCascata(processo.Declaracao()).Should().NotBeEmpty("a oferta federal tem SegueCascata sem cascata configurada");
        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo)).Should().Contain(item => item.Codigo == "cascata_pendente" && !item.Ok);
    }
}
