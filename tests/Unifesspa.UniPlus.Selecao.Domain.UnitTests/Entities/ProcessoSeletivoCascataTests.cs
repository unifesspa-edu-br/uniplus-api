namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura de <see cref="ProcessoSeletivo.PendenciaDaCascata"/> e do item
/// "Cascata de remanejamento" em <see cref="ProcessoSeletivo.AvaliarConformidade"/>
/// (RN-CASCATA-1/2/2b/3, Story #575) — a validação cross-dimensão que varre
/// <c>DistribuicaoVagas</c> por oferta, fora do agregador genérico de
/// conformidade (<see cref="ProcessoSeletivo.PendenciaDeConformidade"/>).
/// </summary>
public sealed class ProcessoSeletivoCascataTests
{
    private static readonly string HashFixo = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];

    private static ReferenciaRegra RegraLei12711() =>
        ReferenciaRegra.Criar(RegraDistribuicaoVagasCodigo.Lei12711, "v1", HashFixo).Value!;

    private static ReferenciaRegra RegraInstitucional() =>
        ReferenciaRegra.Criar(RegraDistribuicaoVagasCodigo.Institucional, "v1", HashFixo).Value!;

    private static ReferenciaRegra RegraAjuste() =>
        ReferenciaRegra.Criar("RECONCILIACAO-VAGAS-ART11-PU", "v1", HashFixo).Value!;

    private static ReferenciaReservaDemograficaSnapshot Demografica() =>
        ReferenciaReservaDemograficaSnapshot.Criar(Guid.CreateVersion7(), "2022", 79m, 1.5m, 8.5m, "Censo 2022").Value!;

    private static ReferenciaRegra RegraCascata() =>
        ReferenciaRegra.Criar(RegraRemanejamentoCodigo.Cascata, "v1", HashFixo).Value!;

    private static ModalidadeSelecionada Modalidade(
        string codigo, NaturezaLegalModalidade natureza, ComposicaoVagasModalidade composicao, int? quantidadeDeclarada = null) =>
        ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), codigo, null, natureza, composicao,
            composicaoOrigemCodigo: null,
            natureza == NaturezaLegalModalidade.CotaReservada ? RegraRemanejamentoModalidade.SegueCascata : RegraRemanejamentoModalidade.Nenhuma,
            null, null, null, [], null, "base legal", quantidadeDeclarada).Value!;

    private static List<ModalidadeSelecionada> AsOitoFederaisMaisAc() =>
    [
        Modalidade(ModalidadesFederaisLei12711.LbPpi, NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDoVr),
        Modalidade(ModalidadesFederaisLei12711.LbQ, NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDoVr),
        Modalidade(ModalidadesFederaisLei12711.LbPcd, NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDoVr),
        Modalidade(ModalidadesFederaisLei12711.LbEp, NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDoVr),
        Modalidade(ModalidadesFederaisLei12711.LiPpi, NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDoVr),
        Modalidade(ModalidadesFederaisLei12711.LiQ, NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDoVr),
        Modalidade(ModalidadesFederaisLei12711.LiPcd, NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDoVr),
        Modalidade(ModalidadesFederaisLei12711.LiEp, NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDoVr),
        Modalidade(ModalidadesFederaisLei12711.Ac, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo),
    ];

    private static ConfiguracaoDistribuicaoVagas OfertaFederalCompleta() =>
        ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 50, pr: 0.5m, RegraLei12711(), RegraAjuste(), Demografica(), AsOitoFederaisMaisAc()).Value!;

    private static DestinoRemanejamento Destino(string origem, int ordem, string destino) =>
        DestinoRemanejamento.Criar(origem, ordem, destino).Value!;

    /// <summary>A matriz legal completa (8×7), fallback AC — a mesma semeada em REMANEJ-CASCATA-LEI-12711 v1.</summary>
    private static ConfiguracaoCascataRemanejamento CascataCompleta()
    {
        string[] origens =
        [
            ModalidadesFederaisLei12711.LbPpi, ModalidadesFederaisLei12711.LbQ, ModalidadesFederaisLei12711.LbPcd, ModalidadesFederaisLei12711.LbEp,
            ModalidadesFederaisLei12711.LiPpi, ModalidadesFederaisLei12711.LiQ, ModalidadesFederaisLei12711.LiPcd, ModalidadesFederaisLei12711.LiEp,
        ];
        List<DestinoRemanejamento> destinos = [];
        foreach (string origem in origens)
        {
            string[] ordemDestinos = [.. origens.Where(o => o != origem)];
            for (int i = 0; i < ordemDestinos.Length; i++)
            {
                destinos.Add(Destino(origem, i + 1, ordemDestinos[i]));
            }
        }

        return ConfiguracaoCascataRemanejamento.Criar(RegraCascata(), ModalidadesFederaisLei12711.Ac, destinos).Value!;
    }

    /// <summary>
    /// A matriz legal completa, com TODOS os sete destinos de <paramref name="origemAlvo"/>
    /// substituídos por códigos fora da oferta — as outras sete origens continuam com a fila
    /// legal completa e resolvível. Substituir só UM destino não bastaria: os outros seis da
    /// mesma origem continuariam resolvíveis (o gate usa <c>Any</c> sobre a fila inteira da
    /// origem), então a origem-alvo precisa ficar SEM nenhum destino resolvível na oferta.
    /// </summary>
    private static ConfiguracaoCascataRemanejamento CascataComOrigemSemDestinoResolvivel(string origemAlvo)
    {
        string[] origens =
        [
            ModalidadesFederaisLei12711.LbPpi, ModalidadesFederaisLei12711.LbQ, ModalidadesFederaisLei12711.LbPcd, ModalidadesFederaisLei12711.LbEp,
            ModalidadesFederaisLei12711.LiPpi, ModalidadesFederaisLei12711.LiQ, ModalidadesFederaisLei12711.LiPcd, ModalidadesFederaisLei12711.LiEp,
        ];
        List<DestinoRemanejamento> destinos = [];
        foreach (string origem in origens)
        {
            if (origem == origemAlvo)
            {
                for (int i = 1; i <= 7; i++)
                {
                    destinos.Add(Destino(origem, i, $"FORA_DA_OFERTA_{i}"));
                }

                continue;
            }

            string[] ordemDestinos = [.. origens.Where(o => o != origem)];
            for (int i = 0; i < ordemDestinos.Length; i++)
            {
                destinos.Add(Destino(origem, i + 1, ordemDestinos[i]));
            }
        }

        return ConfiguracaoCascataRemanejamento.Criar(RegraCascata(), ModalidadesFederaisLei12711.Ac, destinos).Value!;
    }

    private static ProcessoSeletivo NovoProcessoComOferta(ConfiguracaoDistribuicaoVagas oferta)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        processo.DefinirDistribuicaoVagas([oferta], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        return processo;
    }

    [Fact(DisplayName = "PendenciaDaCascata com oferta federal completa e cascata legal completa é null (Ok)")]
    public void PendenciaDaCascata_OfertaFederalComCascataCompleta_Ok()
    {
        ProcessoSeletivo processo = NovoProcessoComOferta(OfertaFederalCompleta());
        processo.DefinirCascataRemanejamento(CascataCompleta(), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.PendenciaDaCascata().Should().BeNull();
    }

    [Fact(DisplayName = "PendenciaDaCascata com oferta federal e SEM cascata configurada recusa com CascataOrigemAusente, nomeando oferta e modalidade")]
    public void PendenciaDaCascata_OfertaFederalSemCascata_RecusaComOrigemAusente()
    {
        ConfiguracaoDistribuicaoVagas oferta = OfertaFederalCompleta();
        ProcessoSeletivo processo = NovoProcessoComOferta(oferta);

        DomainError? pendencia = processo.PendenciaDaCascata();

        pendencia.Should().NotBeNull();
        pendencia!.Code.Should().Be("ProcessoSeletivo.CascataOrigemAusente");
        pendencia.Message.Should().Contain(oferta.OfertaCursoOrigemId.ToString());
    }

    [Fact(DisplayName = "PendenciaDaCascata com SegueCascata fora do regime federal (institucional) recusa com CascataForaDoRegimeFederal")]
    public void PendenciaDaCascata_ForaDoRegimeFederal_Recusa()
    {
        ModalidadeSelecionada cotaFederalSobRegimeErrado = Modalidade(
            "LB_PPI", NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.SuplementarAoTotal, quantidadeDeclarada: 2);
        ModalidadeSelecionada ampla = Modalidade("AC", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo, quantidadeDeclarada: 8);
        ConfiguracaoDistribuicaoVagas ofertaInstitucional = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 10, pr: 1m, RegraInstitucional(), regraAjuste: null, referenciaDemografica: null,
            [cotaFederalSobRegimeErrado, ampla]).Value!;
        ProcessoSeletivo processo = NovoProcessoComOferta(ofertaInstitucional);

        DomainError? pendencia = processo.PendenciaDaCascata();

        pendencia.Should().NotBeNull();
        pendencia!.Code.Should().Be("ProcessoSeletivo.CascataForaDoRegimeFederal");
    }

    [Fact(DisplayName = "PendenciaDaCascata com fallback da cascata fora das modalidades da oferta recusa com CascataFallbackNaoSelecionadoNaOferta")]
    public void PendenciaDaCascata_FallbackForaDaOferta_Recusa()
    {
        ProcessoSeletivo processo = NovoProcessoComOferta(OfertaFederalCompleta());
        ConfiguracaoCascataRemanejamento cascataComFallbackInexistente = ConfiguracaoCascataRemanejamento.Criar(
            RegraCascata(), "FALLBACK_INEXISTENTE",
            [Destino(ModalidadesFederaisLei12711.LbPpi, 1, ModalidadesFederaisLei12711.LbQ)]).Value!;
        processo.DefinirCascataRemanejamento(cascataComFallbackInexistente, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        DomainError? pendencia = processo.PendenciaDaCascata();

        pendencia.Should().NotBeNull();
        pendencia!.Code.Should().Be("ProcessoSeletivo.CascataFallbackNaoSelecionadoNaOferta");
    }

    [Fact(DisplayName = "PendenciaDaCascata com destino da origem fora das modalidades da oferta recusa com CascataFallbackNaoSelecionadoNaOferta")]
    public void PendenciaDaCascata_DestinoDaOrigemNaoResolvivelNaOferta_Recusa()
    {
        // As outras sete origens mantêm a fila legal completa (todas resolvíveis nesta
        // oferta) — só o 1º destino de LB_PPI é trocado por um código fora da oferta,
        // isolando a asserção a essa única origem.
        ProcessoSeletivo processo = NovoProcessoComOferta(OfertaFederalCompleta());
        ConfiguracaoCascataRemanejamento cascataComDestinoForaDaOferta = CascataComOrigemSemDestinoResolvivel(ModalidadesFederaisLei12711.LbPpi);
        processo.DefinirCascataRemanejamento(cascataComDestinoForaDaOferta, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        DomainError? pendencia = processo.PendenciaDaCascata();

        pendencia.Should().NotBeNull();
        pendencia!.Code.Should().Be("ProcessoSeletivo.CascataFallbackNaoSelecionadoNaOferta");
    }

    [Fact(DisplayName = "PendenciaDaCascata com origem que nenhuma oferta marca como SegueCascata recusa com CascataOrigemNaoSegueCascata")]
    public void PendenciaDaCascata_OrigemNaoSegueCascataEmNenhumaOferta_Recusa()
    {
        // Só AC (ampla) na oferta — nenhuma modalidade SegueCascata, então o loop por
        // oferta não recusa nada; a checagem global (origens declaradas na cascata vs.
        // origens SegueCascata em alguma oferta) é quem detecta a origem morta.
        ModalidadeSelecionada ampla = Modalidade(ModalidadesFederaisLei12711.Ac, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo, quantidadeDeclarada: 10);
        ConfiguracaoDistribuicaoVagas oferta = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 10, pr: 1m, RegraInstitucional(), regraAjuste: null, referenciaDemografica: null, [ampla]).Value!;
        ProcessoSeletivo processo = NovoProcessoComOferta(oferta);
        ConfiguracaoCascataRemanejamento cascataComOrigemMorta = ConfiguracaoCascataRemanejamento.Criar(
            RegraCascata(), ModalidadesFederaisLei12711.Ac,
            [Destino(ModalidadesFederaisLei12711.LbPpi, 1, ModalidadesFederaisLei12711.Ac)]).Value!;
        processo.DefinirCascataRemanejamento(cascataComOrigemMorta, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        DomainError? pendencia = processo.PendenciaDaCascata();

        pendencia.Should().NotBeNull();
        pendencia!.Code.Should().Be("ProcessoSeletivo.CascataOrigemNaoSegueCascata");
    }

    [Fact(DisplayName = "PendenciaDaCascata com destino que não é modalidade de nenhuma oferta recusa com CascataDestinoDesconhecido")]
    public void PendenciaDaCascata_DestinoDesconhecidoEmTodoOProcesso_Recusa()
    {
        // LB_PPI mantém os 7 destinos legais (todos resolvíveis nesta oferta — o
        // per-oferta loop não recusa) e ganha um 8º destino apontando para um código
        // que não é modalidade de NENHUMA oferta do processo — só a checagem GLOBAL
        // (que roda depois de todas as ofertas passarem) alcança esse caso. LB_Q perde
        // um destino legal (fica com 6, ainda contíguo a partir de 1) para abrir espaço
        // sob o teto de 56 destinos (7×6 + 6 + 8 = 56) sem comprometer a resolubilidade
        // de LB_Q nesta oferta (os 6 restantes continuam todos selecionados).
        ProcessoSeletivo processo = NovoProcessoComOferta(OfertaFederalCompleta());
        string[] origens =
        [
            ModalidadesFederaisLei12711.LbPpi, ModalidadesFederaisLei12711.LbQ, ModalidadesFederaisLei12711.LbPcd, ModalidadesFederaisLei12711.LbEp,
            ModalidadesFederaisLei12711.LiPpi, ModalidadesFederaisLei12711.LiQ, ModalidadesFederaisLei12711.LiPcd, ModalidadesFederaisLei12711.LiEp,
        ];
        List<DestinoRemanejamento> destinos = [];
        foreach (string origem in origens)
        {
            string[] ordemDestinos = [.. origens.Where(o => o != origem)];
            int quantosManter = origem == ModalidadesFederaisLei12711.LbQ ? ordemDestinos.Length - 1 : ordemDestinos.Length;
            for (int i = 0; i < quantosManter; i++)
            {
                destinos.Add(Destino(origem, i + 1, ordemDestinos[i]));
            }

            if (origem == ModalidadesFederaisLei12711.LbPpi)
            {
                destinos.Add(Destino(origem, quantosManter + 1, "DESTINO_QUE_NAO_EXISTE"));
            }
        }

        destinos.Should().HaveCount(56);
        Result<ConfiguracaoCascataRemanejamento> resultadoCascata = ConfiguracaoCascataRemanejamento.Criar(
            RegraCascata(), ModalidadesFederaisLei12711.Ac, destinos);
        resultadoCascata.IsSuccess.Should().BeTrue(resultadoCascata.Error?.Message);
        processo.DefinirCascataRemanejamento(resultadoCascata.Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        DomainError? pendencia = processo.PendenciaDaCascata();

        pendencia.Should().NotBeNull();
        pendencia!.Code.Should().Be("ProcessoSeletivo.CascataDestinoDesconhecido");
    }

    [Fact(DisplayName = "AvaliarConformidade com processo só de ampla concorrência (sem SegueCascata) tem o item Ok, sem cascata")]
    public void AvaliarConformidade_SemModalidadeSegueCascata_ItemCascataOk()
    {
        ModalidadeSelecionada ampla = Modalidade(ModalidadesFederaisLei12711.Ac, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo, quantidadeDeclarada: 10);
        ConfiguracaoDistribuicaoVagas oferta = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 10, pr: 1m, RegraInstitucional(), regraAjuste: null, referenciaDemografica: null, [ampla]).Value!;
        ProcessoSeletivo processo = NovoProcessoComOferta(oferta);

        processo.PendenciaDaCascata().Should().BeNull();
        processo.AvaliarConformidade().Should().Contain(item => item.Item == "Cascata de remanejamento" && item.Ok);
    }

    [Fact(DisplayName = "AvaliarConformidade com oferta federal sem cascata tem o item Cascata de remanejamento não-Ok")]
    public void AvaliarConformidade_OfertaFederalSemCascata_ItemCascataNaoOk()
    {
        ProcessoSeletivo processo = NovoProcessoComOferta(OfertaFederalCompleta());

        processo.AvaliarConformidade().Should().Contain(item => item.Item == "Cascata de remanejamento" && !item.Ok);
    }

    [Fact(DisplayName = "PendenciaDeConformidade (agregador genérico) fica Ok mesmo com a cascata pendente — só PendenciaDaCascata recusa")]
    public void PendenciaDeConformidade_IgnoraAusenciaDaCascata()
    {
        // Processo estruturalmente conforme (etapas, atendimento, classificação,
        // cronograma) — mas com oferta federal SEM cascata configurada. Antes da
        // separação dos checklists, o item "Cascata de remanejamento" entrava na
        // lista que o agregador genérico varre, e essa ausência derrubava
        // PendenciaDeConformidade() com ConformidadeInsuficiente — mascarando o erro
        // nomeado que PendenciaDaCascata() (e CA-04) prometem.
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        processo.DefinirEtapas(
            [EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva").Value!, peso: 1m, ordem: 1).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirDistribuicaoVagas([OfertaFederalCompleta()], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirClassificacao(
            ConfiguracaoClassificacao.Criar(
                regraCalculo: ReferenciaRegra.Criar(RegraCalculoCodigo.ClassificacaoImportada, "v1", HashFixo).Value!,
                regraArredondamento: null, casasArredondamento: null,
                regraOrdemAlocacao: ReferenciaRegra.Criar(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", HashFixo).Value!,
                nOpcoesAlocacao: 1, regrasEliminacao: [], baseadoEmEnem: false).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirCronogramaFases(
            [FaseCronograma.Criar(
                ordem: 1, faseCanonicaOrigemId: Guid.CreateVersion7(), codigo: "RESULTADO_FINAL", donoInstitucional: "CEPS",
                origemData: OrigemDataFase.Propria, agrupaEtapas: true, permiteComplementacao: false, produzResultado: true,
                resultadoDefinitivo: true, coletaInscricao: true,
                inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
                atoProduzidoCodigo: "RESULTADO_FINAL", atoProduzidoEfeitoIrreversivel: false, bancasRequeridas: [], regraRecurso: null).Value!],
            [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        // Issue #1112: item estrutural novo — declarado para manter os demais itens Ok, já que
        // este teste isola a ausência de cascata como a ÚNICA pendência.
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null, confirmacaoFundamentos: false).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.PendenciaDeConformidade().Should().BeNull("os itens estruturais estão todos completos — a cascata não é um deles");
        processo.PendenciaDaCascata().Should().NotBeNull("a oferta federal tem SegueCascata sem cascata configurada");
        processo.AvaliarConformidade().Should().Contain(item => item.Item == "Cascata de remanejamento" && !item.Ok);
    }
}
