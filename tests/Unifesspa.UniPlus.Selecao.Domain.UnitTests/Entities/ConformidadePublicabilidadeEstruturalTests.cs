namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Reflection;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A matriz da issue #1092: pareia cada um dos códigos de recusa alcançáveis nos quatro gates
/// estruturais (<c>PendenciaDeConformidade</c>, <c>PendenciaDoCronograma</c>,
/// <c>PendenciaDaCascata</c>, <c>PendenciaPreCanonicalizacao</c>) com o item vermelho
/// correspondente em <see cref="ProcessoSeletivo.AvaliarConformidade"/>, e confirma que
/// <see cref="ProcessoSeletivo.Publicar"/> continua recusando com o MESMO <c>DomainError</c> —
/// coletar todos os vereditos para exibição não pode mudar qual é a primeira falha.
/// </summary>
/// <remarks>
/// Cada teste isola UMA razão a partir de um processo estruturalmente conforme
/// (<see cref="ProcessoConforme"/>) — as contraprovas ao final cobrem os ramos mais fáceis de
/// confundir com falso vermelho (SiSU/classificação importada sem etapa, cronograma coerente,
/// cascata não aplicável, ausência de gatilho por faixa etária).
/// </remarks>
public sealed class ConformidadePublicabilidadeEstruturalTests
{
    private static readonly string HashFixo = new('a', 64);

    private static ReferenciaRegra Regra(string codigo, char semente) =>
        ReferenciaRegra.Criar(codigo, "v1", new string(semente, 64)).Value!;

    private static DadosEdital Dados() => DadosEdital.Criar(
        "001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)), new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)), Guid.CreateVersion7()).Value!;

    private static Result<VersaoConfiguracao> Publicar(ProcessoSeletivo processo) =>
        Publicar(processo, ContextoDeContagemDePrazos.SemCalendario);

    private static Result<VersaoConfiguracao> Publicar(
        ProcessoSeletivo processo, ContextoDeContagemDePrazos contexto) => processo.Publicar(
        Dados(), "{}"u8.ToArray(), "1.1", "canonical-json/sha256@v1", HashFixo, "teste", TimeProvider.System, contexto);

    private static ConfiguracaoClassificacao ClassificacaoImportada() => ConfiguracaoClassificacao.Criar(
        Regra(RegraCalculoCodigo.ClassificacaoImportada, 'b'), null, null,
        Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, 'c'), 1, [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: []).Value!;

    private static ModalidadeSelecionada Modalidade(
        string codigo,
        NaturezaLegalModalidade natureza,
        RegraRemanejamentoModalidade remanejamento,
        int? quantidadeDeclarada,
        string? acaoQuandoIndeferido = null) =>
        ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), codigo, null, natureza,
            natureza == NaturezaLegalModalidade.Ampla ? ComposicaoVagasModalidade.ResidualDoVo : ComposicaoVagasModalidade.DentroDoVr,
            null, remanejamento, null, null, null, [], acaoQuandoIndeferido, "base legal", quantidadeDeclarada).Value!;

    private static readonly (string? Codigo, string? Rotulo) GrupoTecnologica = ("TECNOLOGICA", "Tecnológica");

    private static ConfiguracaoDistribuicaoVagas DistribuicaoAmpla(int voBase, string? acaoQuandoIndeferido = null) =>
        OfertaAmpla(voBase, grupoAreaEnem: null, acaoQuandoIndeferido);

    private static ConfiguracaoDistribuicaoVagas OfertaAmpla(
        int voBase, (string? Codigo, string? Rotulo)? grupoAreaEnem, string? acaoQuandoIndeferido = null) =>
        ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase, pr: 1m,
            Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'), regraAjuste: null, referenciaDemografica: null,
            [Modalidade("AC", NaturezaLegalModalidade.Ampla, RegraRemanejamentoModalidade.Nenhuma, voBase, acaoQuandoIndeferido)],
            grupoAreaEnem: grupoAreaEnem).Value!;

    /// <summary>Fase mínima e coerente: não agrupa etapas (dispensável sem prova), produz resultado, não coleta inscrição.</summary>
    private static FaseCronograma FaseBase(bool coletaInscricao = false) => FaseCronograma.Criar(
        1, Guid.CreateVersion7(), "RESULTADO_FINAL", "CEPS", OrigemDataFase.Delegada,
        agrupaEtapas: false, permiteComplementacao: false, coletaInscricao, coletaSolicitacaoIsencao: false, inicio: null, fim: null,
        produtos: [ProdutoDaFase.Criar("RESULTADO_FINAL", PapelProdutoFase.Definitivo)],
        faseConcluinteCodigo: null,
        emiteParecerIndividual: false,
        bancasRequeridas: [], regraRecurso: null).Value!;

    /// <summary>
    /// Processo estruturalmente conforme aos QUATRO gates: sem etapa (ImportacaoExterna +
    /// ClassificacaoImportada dispensam prova), atendimento, distribuição, classificação e
    /// cronograma coerente com uma única fase que produz resultado. Sem cascata (nenhuma
    /// modalidade SegueCascata) e sem exigências/regras de derivação/fatos coletados — os quatro
    /// gates ficam vazios/Ok até que o teste acrescente a única razão que quer isolar.
    /// </summary>
    private static ProcessoSeletivo ProcessoConforme(IReadOnlyList<ConfiguracaoDistribuicaoVagas>? distribuicao = null)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Matriz Estrutural", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(),
            Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar(
                "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirDistribuicaoVagas(distribuicao ?? [DistribuicaoAmpla(10)], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirClassificacao(ClassificacaoImportada(), PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirCronogramaFases([FaseBase()], [], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        // Issue #1112: publicar sem declarar cobrança de taxa é recusado (CA-01) — declarada
        // aqui para manter os QUATRO gates estruturais vazios/Ok até o teste acrescentar a
        // única razão que quer isolar.
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        return processo;
    }

    private static DocumentoExigidoBaseLegal BaseLegalResolvida() => DocumentoExigidoBaseLegal.Criar(
        "Lei 12.711/2012, art. 3º", TipoAbrangencia.InternaEdital, StatusBaseLegal.Resolvido, null).Value!;

    private static NoExigenciaBaseLegal BaseLegalDoGrupo(StatusBaseLegal status) =>
        NoExigenciaBaseLegal.Criar("Lei 12.711/2012, art. 3º", TipoAbrangencia.Federal, status, null).Value!;

    // ══════════════════════════════════════════════════════════════════════════════════════
    // PendenciaDoCronograma — quatro razões (§3.4/§3.5)
    // ══════════════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "Cronograma: fase de avaliação sem etapa pontuada — item vermelho e Publicar recusa com AvaliacaoSemEtapa")]
    public void Cronograma_FaseDeAvaliacaoSemEtapa()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        // DefinirCronogramaFases bloqueia EAGERLY uma fase que agrupa etapas sem etapa nenhuma —
        // por isso a etapa é definida ANTES (torna a fase agrupa-etapas válida na hora), e só
        // DEPOIS removida via DefinirEtapas([]): a fase de avaliação fica órfã, e essa direção só
        // o gate de publicação (LAZY) pega — é exatamente o defeito que o docblock de
        // PendenciaDoCronograma descreve como "defesa em profundidade".
        processo.DefinirEtapas(
            [EtapaProcesso.Criar("Prova", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, peso: 1m, ordem: 1).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();
        processo.DefinirCronogramaFases(
            [FaseCronograma.Criar(
                1, Guid.CreateVersion7(), "RESULTADO_FINAL", "CEPS", OrigemDataFase.Delegada,
                agrupaEtapas: true, permiteComplementacao: false,                 coletaInscricao: false, coletaSolicitacaoIsencao: false, inicio: null, fim: null,
                produtos: [ProdutoDaFase.Criar("RESULTADO_FINAL", PapelProdutoFase.Definitivo)],
                faseConcluinteCodigo: null,
                emiteParecerIndividual: false,
                bancasRequeridas: [], regraRecurso: null).Value!],
            [], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        processo.DefinirEtapas([], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario).Should().ContainSingle(i => !i.Ok)
            .Which.Codigo.Should().Be("cronograma_fase_agrupadora_sem_etapa_pontuada");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.AvaliacaoSemEtapa");
    }

    [Fact(DisplayName = "Cronograma: etapa pontuada sem fase de avaliação — item vermelho e Publicar recusa com EtapaSemFaseDeAvaliacao")]
    public void Cronograma_EtapaSemFaseDeAvaliacao()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        processo.DefinirEtapas(
            [EtapaProcesso.Criar("Prova", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, peso: 1m, ordem: 1).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();
        // Fase permanece sem agrupar etapas (herdada de ProcessoConforme).

        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario).Should().ContainSingle(i => !i.Ok)
            .Which.Codigo.Should().Be("cronograma_etapa_pontuada_sem_fase_agrupadora");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.EtapaSemFaseDeAvaliacao");
    }

    [Fact(DisplayName = "Cronograma: InscricaoPropria sem fase de coleta — item vermelho e Publicar recusa com InscricaoPropriaSemFaseDeColeta (cenário da issue)")]
    public void Cronograma_InscricaoPropriaSemFaseDeColeta()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Matriz Estrutural", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar(
                "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirDistribuicaoVagas([DistribuicaoAmpla(10)], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirClassificacao(ClassificacaoImportada(), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirCronogramaFases([FaseBase(coletaInscricao: false)], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario).Should().ContainSingle(i => !i.Ok)
            .Which.Codigo.Should().Be("cronograma_inscricao_propria_sem_fase_de_coleta");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.InscricaoPropriaSemFaseDeColeta");
    }

    [Fact(DisplayName = "Cronograma: fase que coleta inscrição sem janela — item vermelho e Publicar recusa com FaseQueColetaInscricaoSemJanela")]
    public void Cronograma_FaseQueColetaInscricaoSemJanela()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Matriz Estrutural", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar(
                "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirDistribuicaoVagas([DistribuicaoAmpla(10)], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirClassificacao(ClassificacaoImportada(), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        // A fase coleta inscrição e é DELEGADA, então a janela é opcional para o cadastro (§3.2)
        // — mas não para publicar: é dela que o Edital tira o prazo (issue #1350).
        processo.DefinirCronogramaFases([FaseBase(coletaInscricao: true)], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario).Should().ContainSingle(i => !i.Ok)
            .Which.Codigo.Should().Be("cronograma_fase_que_coleta_inscricao_sem_janela");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.FaseQueColetaInscricaoSemJanela");
    }

    [Fact(DisplayName = "Cronograma: vagas ofertadas sem fase que produz resultado — item vermelho e Publicar recusa com VagasSemFaseQueProduzResultado")]
    public void Cronograma_VagasSemFaseQueProduzResultado()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        processo.DefinirCronogramaFases(
            [FaseCronograma.Criar(
                1, Guid.CreateVersion7(), "MATRICULA", "CEPS", OrigemDataFase.Delegada,
                agrupaEtapas: false, permiteComplementacao: false,                 coletaInscricao: false, coletaSolicitacaoIsencao: false, inicio: null, fim: null,
                produtos: [], faseConcluinteCodigo: null, emiteParecerIndividual: false,
                bancasRequeridas: [], regraRecurso: null).Value!],
            [], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario).Should().ContainSingle(i => !i.Ok)
            .Which.Codigo.Should().Be("cronograma_vagas_sem_fase_que_produz_resultado");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.VagasSemFaseQueProduzResultado");
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // PendenciaDaCascata — cinco razões (RN-CASCATA-1/2/2b/3)
    // ══════════════════════════════════════════════════════════════════════════════════════

    private static ReferenciaRegra RegraLei12711() => Regra(RegraDistribuicaoVagasCodigo.Lei12711, 'f');

    private static ReferenciaRegra RegraCascataRemanejamento() => Regra(RegraRemanejamentoCodigo.Cascata, 'e');

    private static DestinoRemanejamento Destino(string origem, int ordem, string destino) =>
        DestinoRemanejamento.Criar(origem, ordem, destino).Value!;

    private static ReferenciaReservaDemograficaSnapshot Demografica() =>
        ReferenciaReservaDemograficaSnapshot.Criar(Guid.CreateVersion7(), "2022", 79m, 1.5m, 8.5m, "Censo 2022").Value!;

    private static readonly string[] OrigensFederais =
    [
        ModalidadesFederaisLei12711.LbPpi, ModalidadesFederaisLei12711.LbQ, ModalidadesFederaisLei12711.LbPcd, ModalidadesFederaisLei12711.LbEp,
        ModalidadesFederaisLei12711.LiPpi, ModalidadesFederaisLei12711.LiQ, ModalidadesFederaisLei12711.LiPcd, ModalidadesFederaisLei12711.LiEp,
    ];

    /// <summary>
    /// As 8 modalidades federais (SegueCascata) + AC — INV-6 exige o conjunto completo sob Lei
    /// 12.711. Sem <c>quantidadeDeclarada</c>: sob o ramo federal, DENTRO_DO_VR e RESIDUAL_DO_VO
    /// são CALCULADAS pela fórmula do art. 10 — declarar quantidade aqui é recusado
    /// (QuantidadeCalculadaNaoInformavel).
    /// </summary>
    private static List<ModalidadeSelecionada> AsOitoFederaisMaisAc() =>
    [
        .. OrigensFederais.Select(codigo =>
            Modalidade(codigo, NaturezaLegalModalidade.CotaReservada, RegraRemanejamentoModalidade.SegueCascata, quantidadeDeclarada: null)),
        Modalidade(ModalidadesFederaisLei12711.Ac, NaturezaLegalModalidade.Ampla, RegraRemanejamentoModalidade.Nenhuma, quantidadeDeclarada: null),
    ];

    private static ReferenciaRegra RegraAjusteArt11() => Regra("RECONCILIACAO-VAGAS-ART11-PU", 'd');

    private static ConfiguracaoDistribuicaoVagas OfertaFederalCompleta() => ConfiguracaoDistribuicaoVagas.Criar(
        Guid.CreateVersion7(), voBase: 40, pr: 0.5m, RegraLei12711(), RegraAjusteArt11(), Demografica(), AsOitoFederaisMaisAc()).Value!;

    /// <summary>A matriz legal completa (8×7), fallback AC — cada origem resolve nas outras sete, todas ofertadas.</summary>
    private static ConfiguracaoCascataRemanejamento CascataCompleta()
    {
        List<DestinoRemanejamento> destinos = [];
        foreach (string origem in OrigensFederais)
        {
            string[] ordemDestinos = [.. OrigensFederais.Where(o => o != origem)];
            for (int i = 0; i < ordemDestinos.Length; i++)
            {
                destinos.Add(Destino(origem, i + 1, ordemDestinos[i]));
            }
        }

        return ConfiguracaoCascataRemanejamento.Criar(RegraCascataRemanejamento(), ModalidadesFederaisLei12711.Ac, destinos).Value!;
    }

    /// <summary>Só os itens de <see cref="ProcessoSeletivo.AvaliarConformidade"/> que se espera ver vermelhos.</summary>
    private static void SoEstesItensVermelhos(ProcessoSeletivo processo, params string[] itensVermelhos) =>
        SoEstesItensVermelhos(processo, ContextoDeContagemDePrazos.SemCalendario, itensVermelhos);

    private static void SoEstesItensVermelhos(
        ProcessoSeletivo processo, ContextoDeContagemDePrazos contexto, params string[] itensVermelhos) =>
        processo.AvaliarConformidade(contexto).Where(static i => !i.Ok).Select(static i => i.Codigo)
            .Should().BeEquivalentTo(itensVermelhos);

    [Fact(DisplayName = "Cascata: modalidade SegueCascata fora do regime federal — item vermelho e Publicar recusa com CascataForaDoRegimeFederal")]
    public void Cascata_ForaDoRegimeFederal()
    {
        ConfiguracaoDistribuicaoVagas oferta = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 10, pr: 1m,
            Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'), regraAjuste: null, referenciaDemografica: null,
            [
                Modalidade("AC", NaturezaLegalModalidade.Ampla, RegraRemanejamentoModalidade.Nenhuma, 8),
                Modalidade("LB_PPI", NaturezaLegalModalidade.CotaReservada, RegraRemanejamentoModalidade.SegueCascata, 2),
            ]).Value!;
        ProcessoSeletivo processo = ProcessoConforme([oferta]);

        // A cascata (item agregado, comportamento pré-existente) TAMBÉM vai a vermelho —
        // ela já cobria este código antes da issue #1092; o item NOVO é o detalhamento.
        SoEstesItensVermelhos(
            processo, "cascata_pendente", "cascata_modalidade_fora_do_regime_federal");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.CascataForaDoRegimeFederal");
    }

    [Fact(DisplayName = "Cascata: oferta federal SegueCascata sem cascata configurada — item vermelho e Publicar recusa com CascataOrigemAusente")]
    public void Cascata_OrigemAusente()
    {
        ProcessoSeletivo processo = ProcessoConforme([OfertaFederalCompleta()]);
        // Nenhuma DefinirCascataRemanejamento — a origem exigida não tem cascata configurada.

        SoEstesItensVermelhos(
            processo, "cascata_pendente", "cascata_origem_ausente");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.CascataOrigemAusente");
    }

    [Fact(DisplayName = "Cascata: fallback fora das modalidades da oferta — item vermelho e Publicar recusa com CascataFallbackNaoSelecionadoNaOferta")]
    public void Cascata_FallbackNaoSelecionadoNaOferta()
    {
        ProcessoSeletivo processo = ProcessoConforme([OfertaFederalCompleta()]);
        ConfiguracaoCascataRemanejamento cascataComFallbackInexistente = ConfiguracaoCascataRemanejamento.Criar(
            RegraCascataRemanejamento(), "FALLBACK_INEXISTENTE",
            [.. CascataCompleta().Destinos]).Value!;
        processo.DefinirCascataRemanejamento(cascataComFallbackInexistente, PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        SoEstesItensVermelhos(
            processo, "cascata_pendente", "cascata_fallback_nao_ofertado");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.CascataFallbackNaoSelecionadoNaOferta");
    }

    [Fact(DisplayName = "Cascata: origem declarada que nenhuma oferta marca SegueCascata — item vermelho e Publicar recusa com CascataOrigemNaoSegueCascata")]
    public void Cascata_OrigemNaoSegueCascata()
    {
        ConfiguracaoDistribuicaoVagas oferta = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 10, pr: 1m,
            Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'), regraAjuste: null, referenciaDemografica: null,
            [Modalidade("AC", NaturezaLegalModalidade.Ampla, RegraRemanejamentoModalidade.Nenhuma, 10)]).Value!;
        ProcessoSeletivo processo = ProcessoConforme([oferta]);
        // Nenhuma modalidade desta oferta é SegueCascata — o loop por oferta não recusa nada; só
        // a checagem global (origem declarada × origens SegueCascata em alguma oferta) alcança.
        processo.DefinirCascataRemanejamento(
            ConfiguracaoCascataRemanejamento.Criar(
                RegraCascataRemanejamento(), "AC", [Destino(ModalidadesFederaisLei12711.LbPpi, 1, "AC")]).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        SoEstesItensVermelhos(
            processo, "cascata_pendente", "cascata_origem_nao_segue_cascata");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.CascataOrigemNaoSegueCascata");
    }

    [Fact(DisplayName = "Cascata: destino declarado que não é modalidade de nenhuma oferta — item vermelho e Publicar recusa com CascataDestinoDesconhecido")]
    public void Cascata_DestinoDesconhecido()
    {
        ProcessoSeletivo processo = ProcessoConforme([OfertaFederalCompleta()]);
        // A matriz legal completa resolve todas as origens dentro da oferta (o loop por oferta
        // não recusa nada); LB_PPI ganha um OITAVO destino (ordem contígua) apontando para um
        // código que não é modalidade de nenhuma oferta — só a checagem GLOBAL, que roda depois
        // do loop por oferta, alcança esse destino sobressalente. LB_Q perde um destino legal
        // (fica com 6, ainda contíguo a partir de 1 — os 6 restantes continuam todos resolvíveis
        // nesta oferta) para abrir espaço sob o teto de 56 destinos (7×7 + 6 + 1 = 56).
        List<DestinoRemanejamento> destinos =
        [
            .. CascataCompleta().Destinos.Where(d => !(d.ModalidadeOrigemCodigo == ModalidadesFederaisLei12711.LbQ && d.Ordem == 7)),
            Destino(ModalidadesFederaisLei12711.LbPpi, 8, "CODIGO_FORA_DA_OFERTA"),
        ];
        processo.DefinirCascataRemanejamento(
            ConfiguracaoCascataRemanejamento.Criar(RegraCascataRemanejamento(), ModalidadesFederaisLei12711.Ac, destinos).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        SoEstesItensVermelhos(
            processo, "cascata_pendente", "cascata_destino_desconhecido");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.CascataDestinoDesconhecido");
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // PendenciaPreCanonicalizacao — doze razões (Story #554/#920/#927/#928)
    // ══════════════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "Pré-canonicalização: exigência CONDICIONAL vazia que determina resultado — item vermelho e Publicar recusa com CondicionalVaziaDeterminaResultado")]
    public void PreCanon_CondicionalVaziaDeterminaResultado()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        Guid faseId = processo.CronogramaFases.Single().Id;
        DocumentoExigido exigencia = DocumentoExigido.Criar(
            faseId, Guid.CreateVersion7(), "CERTIDAO_RESERVISTA", "Certidão de reservista", "MILITAR",
            Aplicabilidade.Condicional, obrigatorio: true, consequenciaIndeferimento: null,
            condicoes: [], basesLegais: [BaseLegalResolvida()], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(exigencia, 0).Value!], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario).Should().ContainSingle(i => !i.Ok)
            .Which.Codigo.Should().Be("exigencia_condicional_vazia_determina_resultado");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoExigido.CondicionalVaziaDeterminaResultado");
    }

    [Fact(DisplayName = "Pré-canonicalização: exigência (folha) REMOVE_VANTAGEM sem vantagem viva — item vermelho e Publicar recusa com DocumentoExigido.RemoveVantagemSemVantagemViva")]
    public void PreCanon_ExigenciaRemoveVantagemSemVantagemViva()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        Guid faseId = processo.CronogramaFases.Single().Id;
        DocumentoExigido exigencia = DocumentoExigido.Criar(
            faseId, Guid.CreateVersion7(), "DOC_GERAL", "Documento geral", "PESSOAL",
            Aplicabilidade.Geral, obrigatorio: true, consequenciaIndeferimento: "REMOVE_VANTAGEM",
            condicoes: [], basesLegais: [BaseLegalResolvida()], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(exigencia, 0).Value!], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        // Sem DefinirBonusRegional — nenhuma vantagem viva para remover.

        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario).Should().ContainSingle(i => !i.Ok)
            .Which.Codigo.Should().Be("exigencia_remove_vantagem_sem_vantagem_viva");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoExigido.RemoveVantagemSemVantagemViva");
    }

    [Fact(DisplayName = "Pré-canonicalização: exigência (folha) com consequência incoerente com a ação da vaga — item vermelho e Publicar recusa com DocumentoExigido.ConsequenciaIncoerenteComAcaoDaVaga")]
    public void PreCanon_ExigenciaConsequenciaIncoerenteComAcaoDaVaga()
    {
        ConfiguracaoDistribuicaoVagas oferta = DistribuicaoAmpla(10, acaoQuandoIndeferido: "RECLASSIFICAR_AC");
        ProcessoSeletivo processo = ProcessoConforme([oferta]);
        Guid faseId = processo.CronogramaFases.Single().Id;
        DocumentoExigido exigencia = DocumentoExigido.Criar(
            faseId, Guid.CreateVersion7(), "DOC_GERAL", "Documento geral", "PESSOAL",
            Aplicabilidade.Geral, obrigatorio: true, consequenciaIndeferimento: "ELIMINA",
            condicoes: [], basesLegais: [BaseLegalResolvida()], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(exigencia, 0).Value!], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario).Should().ContainSingle(i => !i.Ok)
            .Which.Codigo.Should().Be("exigencia_consequencia_incoerente_com_acao_da_vaga");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoExigido.ConsequenciaIncoerenteComAcaoDaVaga");
    }

    [Fact(DisplayName = "Pré-canonicalização: grupo OU/N-de REMOVE_VANTAGEM sem vantagem viva — item vermelho e Publicar recusa com NoExigencia.RemoveVantagemSemVantagemViva")]
    public void PreCanon_GrupoRemoveVantagemSemVantagemViva()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        Guid faseId = processo.CronogramaFases.Single().Id;
        DocumentoExigido documento = DocumentoExigido.Criar(
            faseId, Guid.CreateVersion7(), "DOC_GERAL", "Documento geral", "PESSOAL",
            Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null,
            condicoes: [], basesLegais: [], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;
        NoExigencia folha = NoExigencia.CriarFolha(documento, 0).Value!;
        NoExigencia grupo = NoExigencia.CriarGrupo(
            TipoNo.GrupoOu, 0, 1, "REMOVE_VANTAGEM", [BaseLegalDoGrupo(StatusBaseLegal.Resolvido)], [folha]).Value!;
        processo.DefinirDocumentosExigidos([grupo], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario).Should().ContainSingle(i => !i.Ok)
            .Which.Codigo.Should().Be("grupo_remove_vantagem_sem_vantagem_viva");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.RemoveVantagemSemVantagemViva");
    }

    [Fact(DisplayName = "Pré-canonicalização: grupo OU/N-de com consequência incoerente com a ação da vaga — item vermelho e Publicar recusa com NoExigencia.ConsequenciaIncoerenteComAcaoDaVaga")]
    public void PreCanon_GrupoConsequenciaIncoerenteComAcaoDaVaga()
    {
        ConfiguracaoDistribuicaoVagas oferta = DistribuicaoAmpla(10, acaoQuandoIndeferido: "RECLASSIFICAR_AC");
        ProcessoSeletivo processo = ProcessoConforme([oferta]);
        Guid faseId = processo.CronogramaFases.Single().Id;
        DocumentoExigido documento = DocumentoExigido.Criar(
            faseId, Guid.CreateVersion7(), "DOC_GERAL", "Documento geral", "PESSOAL",
            Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null,
            condicoes: [], basesLegais: [], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;
        NoExigencia folha = NoExigencia.CriarFolha(documento, 0).Value!;
        NoExigencia grupo = NoExigencia.CriarGrupo(
            TipoNo.GrupoOu, 0, 1, "ELIMINA", [BaseLegalDoGrupo(StatusBaseLegal.Resolvido)], [folha]).Value!;
        processo.DefinirDocumentosExigidos([grupo], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario).Should().ContainSingle(i => !i.Ok)
            .Which.Codigo.Should().Be("grupo_consequencia_incoerente_com_acao_da_vaga");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.ConsequenciaIncoerenteComAcaoDaVaga");
    }

    private static CondicaoGatilho GatilhoFaixaEtaria() =>
        CondicaoGatilho.Criar(0, "FAIXA_ETARIA", Operador.MaiorIgual, JsonSerializer.SerializeToElement(18)).Value!;

    [Fact(DisplayName = "Pré-canonicalização: gatilho por FAIXA_ETARIA sem referência temporal configurada — item vermelho e Publicar recusa com ReferenciaTemporalFatosAusente")]
    public void PreCanon_ReferenciaTemporalFatosAusente()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        Guid faseId = processo.CronogramaFases.Single().Id;
        DocumentoExigido exigencia = DocumentoExigido.Criar(
            faseId, Guid.CreateVersion7(), "DECLARACAO_MAIORIDADE", "Declaração de maioridade", "PESSOAL",
            Aplicabilidade.Condicional, obrigatorio: true, consequenciaIndeferimento: null,
            condicoes: [GatilhoFaixaEtaria()], basesLegais: [BaseLegalResolvida()], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(exigencia, 0).Value!], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        // Nenhuma ReferenciaTemporalFatos configurada.

        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario).Should().ContainSingle(i => !i.Ok)
            .Which.Codigo.Should().Be("referencia_temporal_ausente_com_gatilho_etario");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.ReferenciaTemporalFatosAusente");
    }

    [Fact(DisplayName = "Pré-canonicalização: referência temporal aponta para fase removida depois — item vermelho e Publicar recusa com ReferenciaTemporalFatosFaseInexistente")]
    public void PreCanon_ReferenciaTemporalFatosFaseInexistente()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        // Duas fases: a que fica com a exigência (sobrevive) e a que ancora a referência
        // temporal (será removida DEPOIS, deixando a referência órfã — §3.5-like, mas para
        // ReferenciaTemporalFatos, que DefinirCronogramaFases não guarda contra remoção).
        FaseCronograma faseComExigencia = FaseBase();
        FaseCronograma faseAncora = FaseCronograma.Criar(
            2, Guid.CreateVersion7(), "HOMOLOGACAO", "CEPS", OrigemDataFase.Propria,
            agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false,
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            produtos: [], faseConcluinteCodigo: null, emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null).Value!;
        processo.DefinirCronogramaFases([faseComExigencia, faseAncora], [], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        DocumentoExigido exigencia = DocumentoExigido.Criar(
            faseComExigencia.Id, Guid.CreateVersion7(), "DECLARACAO_MAIORIDADE", "Declaração de maioridade", "PESSOAL",
            Aplicabilidade.Condicional, obrigatorio: true, consequenciaIndeferimento: null,
            condicoes: [GatilhoFaixaEtaria()], basesLegais: [BaseLegalResolvida()], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(exigencia, 0).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        processo.DefinirReferenciaTemporalFatos(
            ReferenciaTemporalFatos.Criar(ReferenciaTipo.FimFase, null, faseAncora.Id).Value!, PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        // Remove a fase-âncora do cronograma — só faseComExigencia sobrevive (mesma
        // FaseCanonicaOrigemId, então DefinirDocumentosExigidos.ExigidoNaFaseId não fica órfão).
        processo.DefinirCronogramaFases([faseComExigencia], [], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario).Should().ContainSingle(i => !i.Ok)
            .Which.Codigo.Should().Be("referencia_temporal_fase_fora_do_cronograma");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.ReferenciaTemporalFatosFaseInexistente");
    }

    [Fact(DisplayName = "Pré-canonicalização: referência temporal aponta para fase sem o extremo definido — item vermelho e Publicar recusa com ReferenciaTemporalFatosExtremoAusente")]
    public void PreCanon_ReferenciaTemporalFatosExtremoAusente()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        FaseCronograma faseSemExtremo = FaseCronograma.Criar(
            2, Guid.CreateVersion7(), "HOMOLOGACAO", "CEPS", OrigemDataFase.Delegada,
            agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false, inicio: null, fim: null,
            produtos: [], faseConcluinteCodigo: null, emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null).Value!;
        processo.DefinirCronogramaFases([FaseBase(), faseSemExtremo], [], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        Guid faseComExigenciaId = processo.CronogramaFases.Single(f => f.Ordem == 1).Id;
        DocumentoExigido exigencia = DocumentoExigido.Criar(
            faseComExigenciaId, Guid.CreateVersion7(), "DECLARACAO_MAIORIDADE", "Declaração de maioridade", "PESSOAL",
            Aplicabilidade.Condicional, obrigatorio: true, consequenciaIndeferimento: null,
            condicoes: [GatilhoFaixaEtaria()], basesLegais: [BaseLegalResolvida()], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(exigencia, 0).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        processo.DefinirReferenciaTemporalFatos(
            ReferenciaTemporalFatos.Criar(ReferenciaTipo.FimFase, null, faseSemExtremo.Id).Value!, PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario).Should().ContainSingle(i => !i.Ok)
            .Which.Codigo.Should().Be("referencia_temporal_extremo_da_fase_ausente");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.ReferenciaTemporalFatosExtremoAusente");
    }

    [Fact(DisplayName = "Pré-canonicalização: FIM_INSCRICAO sem fase de coleta com Fim definido — item vermelho e Publicar recusa com ReferenciaTemporalFatosFimInscricaoIndisponivel")]
    public void PreCanon_ReferenciaTemporalFatosFimInscricaoIndisponivel()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        Guid faseId = processo.CronogramaFases.Single().Id;
        DocumentoExigido exigencia = DocumentoExigido.Criar(
            faseId, Guid.CreateVersion7(), "DECLARACAO_MAIORIDADE", "Declaração de maioridade", "PESSOAL",
            Aplicabilidade.Condicional, obrigatorio: true, consequenciaIndeferimento: null,
            condicoes: [GatilhoFaixaEtaria()], basesLegais: [BaseLegalResolvida()], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(exigencia, 0).Value!], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        // FaseBase() não coleta inscrição — nenhuma fase resolve FIM_INSCRICAO.
        processo.DefinirReferenciaTemporalFatos(
            ReferenciaTemporalFatos.Criar(ReferenciaTipo.FimInscricao, null, null).Value!, PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario).Should().ContainSingle(i => !i.Ok)
            .Which.Codigo.Should().Be("referencia_temporal_fim_inscricao_indisponivel");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.ReferenciaTemporalFatosFimInscricaoIndisponivel");
    }

    [Fact(DisplayName = "Pré-canonicalização: regra de derivação cita fato que o processo não coleta nem deriva — item vermelho e Publicar recusa com FatoColetadoErrorCodes.PrecondicaoCitaFatoNaoColetado")]
    public void PreCanon_RegraDeDerivacaoCitaFatoNaoColetado()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        CondicaoRegraDerivacao condicao = CondicaoRegraDerivacao.Criar(
            1, "BOGUS", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!;
        RegraDerivacaoConfigurada regra = RegraDerivacaoConfigurada.Criar(0, "X", [condicao]).Value!;
        ConfiguracaoDerivacaoFato configuracao = ConfiguracaoDerivacaoFato.Criar("D1", [regra]).Value!;
        processo.DefinirRegrasDerivacao([configuracao], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue(
            "a definição isolada só valida forma e unicidade — não conhece o universo de fatos do processo");

        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario).Should().ContainSingle(i => !i.Ok)
            .Which.Codigo.Should().Be("derivacao_fatos_citados_inexistentes");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoColetadoErrorCodes.PrecondicaoCitaFatoNaoColetado);
    }

    private static OfertaAtendimentoEspecializado OfertaComCondicaoPcd() => OfertaAtendimentoEspecializado.Criar(
        [OfertaCondicao.Criar(Guid.CreateVersion7(), OfertaAtendimentoEspecializado.CodigoCondicaoPcd, "Pessoa com deficiência")],
        [],
        []).Value!;

    [Fact(DisplayName = "Pré-canonicalização: fato coletável CONDICAO_ATENDIMENTO sem nenhuma condição ofertada — item vermelho e Publicar recusa com FatoColetadoSemValoresOfertados (cenário da issue)")]
    public void PreCanon_FatoColetadoCondicaoAtendimentoSemValoresOfertados()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        // ProcessoConforme já oferta atendimento vazio (sem condições) — só falta o fato coletável.
        FatoColetado fato = FatoColetado.Criar(
            "CONDICAO_ATENDIMENTO", 0, "Você se enquadra em alguma condição de atendimento?",
            TipoRenderizacao.SelecaoMultipla, obrigatorio: false, precondicoes: null).Value!;
        processo.DefinirFatosColetados([fato], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario).Should().ContainSingle(i => !i.Ok)
            .Which.Codigo.Should().Be("fato_coletavel_sem_valores_ofertados");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.FatoColetadoSemValoresOfertados");
    }

    [Fact(DisplayName = "Pré-canonicalização: fato coletável TIPO_DEFICIENCIA sem nenhum tipo ofertado — item vermelho e Publicar recusa com FatoColetadoSemValoresOfertados")]
    public void PreCanon_FatoColetadoTipoDeficienciaSemValoresOfertados()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        // Condição PcD ofertada (pré-requisito da ADR-0067), mas nenhum TipoDeficiencia —
        // só o fato de tipo de deficiência fica sem valor para oferecer ao candidato.
        processo.DefinirOfertaAtendimento(OfertaComCondicaoPcd(), PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        FatoColetado fato = FatoColetado.Criar(
            "TIPO_DEFICIENCIA", 0, "Qual o tipo de deficiência?",
            TipoRenderizacao.SelecaoUnica, obrigatorio: false, precondicoes: null).Value!;
        processo.DefinirFatosColetados([fato], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario).Should().ContainSingle(i => !i.Ok)
            .Which.Codigo.Should().Be("fato_coletavel_sem_valores_ofertados");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.FatoColetadoSemValoresOfertados");
    }

    [Fact(DisplayName = "Pré-canonicalização: MODALIDADE contribui código fora do domínio ofertado — item vermelho e Publicar recusa com RegrasDerivacaoFatoErrorCodes.ContribuiForaDoDominio")]
    public void PreCanon_ModalidadeContribuiForaDoDominio()
    {
        ProcessoSeletivo processo = ProcessoConforme([DistribuicaoAmpla(10)]); // domínio ofertado = { AC }
        RegraDerivacaoConfigurada ancora = RegraDerivacaoConfigurada.Criar(0, "V", condicoes: null).Value!;
        ConfiguracaoDerivacaoFato configuracao = ConfiguracaoDerivacaoFato.Criar("MODALIDADE", [ancora]).Value!;
        processo.DefinirRegrasDerivacao([configuracao], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue(
            "\"V\" é rótulo de exibição de AC_PCD no edital, nunca código de entrada — e não está no domínio ofertado (só AC)");

        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario).Should().ContainSingle(i => !i.Ok)
            .Which.Codigo.Should().Be("derivacao_dominio_de_contribuicao_invalido");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(RegrasDerivacaoFatoErrorCodes.ContribuiForaDoDominio);
    }

    [Fact(DisplayName = "Pré-canonicalização: ciclo entre duas configurações de derivação — item vermelho e Publicar recusa com GrafoDependenciaConjuntaErrorCodes.GrafoConjuntoComCiclo")]
    public void PreCanon_GrafoConjuntoComCiclo()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        CondicaoRegraDerivacao citaD2 = CondicaoRegraDerivacao.Criar(1, "D2", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!;
        CondicaoRegraDerivacao citaD1 = CondicaoRegraDerivacao.Criar(1, "D1", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!;
        ConfiguracaoDerivacaoFato d1 = ConfiguracaoDerivacaoFato.Criar(
            "D1", [RegraDerivacaoConfigurada.Criar(0, "X", [citaD2]).Value!]).Value!;
        ConfiguracaoDerivacaoFato d2 = ConfiguracaoDerivacaoFato.Criar(
            "D2", [RegraDerivacaoConfigurada.Criar(0, "Y", [citaD1]).Value!]).Value!;
        processo.DefinirRegrasDerivacao([d1, d2], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue(
            "a definição isolada só barra código duplicado — o ciclo cruza duas configurações e passa aqui");

        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario).Should().ContainSingle(i => !i.Ok)
            .Which.Codigo.Should().Be("grafo_dependencia_com_ciclo");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(GrafoDependenciaConjuntaErrorCodes.GrafoConjuntoComCiclo);
    }

    [Fact(DisplayName = "Pré-canonicalização: desempate por área do ENEM sob classificação sem quadro de pesos — item vermelho e Publicar recusa nomeando o critério")]
    public void PreCanon_DesempatePorAreaSemQuadro()
    {
        ProcessoSeletivo processo = ProcessoConforme(); // classificação importada: sem quadro de pesos por área
        ConfiguracaoClassificacao classificacao = processo.Classificacao!;

        // As duas gravações recusam este estado; ele só chega por outro caminho — um envelope
        // restaurado ou uma escrita feita fora do agregado. É montado aqui direto no agregado:
        // o desempate gravado enquanto a classificação não existia, e a classificação sem
        // quadro posta depois.
        PropertyInfo propriedadeClassificacao = typeof(ProcessoSeletivo).GetProperty(nameof(ProcessoSeletivo.Classificacao))!;
        propriedadeClassificacao.SetValue(processo, null);
        processo.DefinirCriteriosDesempate(
            [CriterioDesempate.Criar(3, Regra(CriterioDesempateCodigo.MaiorNotaAreaEnem, 'e'), new ArgsDesempateMaiorNotaAreaEnem(["REDACAO"])).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        propriedadeClassificacao.SetValue(processo, classificacao);

        SoEstesItensVermelhos(processo, "desempate_area_enem_sem_quadro");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.DesempateAreaEnemSemQuadro");
        resultado.Error.Message.Should().Contain("ordem 3", "a recusa nomeia o critério que o operador tem de corrigir");
    }

    /// <summary>
    /// Processo conforme com uma etapa que compõe nota e a fase que a agrupa: sem elas o
    /// divisor da média é zero e a publicação recusa antes, por outro item. A oferta tem um
    /// grupo do quadro de teste, que a classificação ENEM com cálculo local exige.
    /// </summary>
    private static ProcessoSeletivo ProcessoConformeComEtapaQueCompoeNota()
    {
        ProcessoSeletivo processo = ProcessoConforme([OfertaAmpla(10, GrupoTecnologica)]);
        processo.DefinirEtapas(
            [EtapaProcesso.Criar("Prova", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, peso: 1m, ordem: 1).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();
        processo.DefinirCronogramaFases(
            [FaseCronograma.Criar(
                1, Guid.CreateVersion7(), "RESULTADO_FINAL", "CEPS", OrigemDataFase.Delegada,
                agrupaEtapas: true, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false, inicio: null, fim: null,
                produtos: [ProdutoDaFase.Criar("RESULTADO_FINAL", PapelProdutoFase.Definitivo)],
                faseConcluinteCodigo: null,
                emiteParecerIndividual: false,
                bancasRequeridas: [], regraRecurso: null).Value!],
            [], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        return processo;
    }

    [Fact(DisplayName = "Pré-canonicalização: mais critérios de desempate que o teto — só o item do teto fica vermelho, e Publicar recusa")]
    public void PreCanon_CriteriosDesempateAcimaDoTeto()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        // Direto na lista: a gravação e a restauração recusam acima do teto, e o gate cobre o
        // rascunho que chega por outro caminho.
        CriteriosDesempateDe(processo).AddRange(Enumerable.Range(1, ProcessoSeletivo.CriteriosDesempateMaximo + 1).Select(static ordem =>
            CriterioDesempate.Criar(ordem, Regra(CriterioDesempateCodigo.MaiorIdade, 'f'), new ArgsDesempateMaiorIdade()).Value!));

        SoEstesItensVermelhos(processo, "criterios_desempate_em_excesso");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.CriteriosDesempateEmExcesso");
    }

    [Theory(DisplayName = "Pré-canonicalização: desempate por área com a ordem de áreas mal formada — só o item da ordem fica vermelho, e Publicar recusa")]
    [InlineData(new string[0], "CriterioDesempate.AreasObrigatorias")]
    [InlineData(new[] { "redacao" }, "CriterioDesempate.AreaInvalida")]
    [InlineData(new[] { "REDACAO", "REDACAO" }, "CriterioDesempate.AreaRepetida")]
    public void PreCanon_DesempatePorAreaComOrdemMalFormada(string[] areas, string codigo)
    {
        ProcessoSeletivo processo = ProcessoConformeComEtapaQueCompoeNota();
        typeof(ProcessoSeletivo).GetProperty(nameof(ProcessoSeletivo.Classificacao))!.SetValue(processo, ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.FormulaMediaPonderada, 'a'), Regra(RegraArredondamentoCodigo.PrecisaoTruncar, 'b'), 2,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, 'c'), 1, [], baseadoEmEnem: true,
            resolucaoPesoAreaEnem: QuadroPesoAreaEnemDeTeste.Resolucao, quadroPesoAreaEnem: QuadroPesoAreaEnemDeTeste.Completo()).Value!);
        // A ordem mal formada não passa por CriterioDesempate.Criar: os args são trocados depois,
        // como num critério que chega ao agregado por outro caminho.
        CriterioDesempate criterio = CriterioDesempate.Criar(
            1, Regra(CriterioDesempateCodigo.MaiorNotaAreaEnem, 'e'), new ArgsDesempateMaiorNotaAreaEnem(["REDACAO"])).Value!;
        typeof(CriterioDesempate).GetProperty(nameof(CriterioDesempate.Args))!.SetValue(criterio, new ArgsDesempateMaiorNotaAreaEnem(areas));
        CriteriosDesempateDe(processo).Add(criterio);

        SoEstesItensVermelhos(processo, "desempate_area_enem_areas_mal_formadas");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(codigo);
        resultado.Error.Message.Should().Contain("ordem 1");
    }

    private static List<CriterioDesempate> CriteriosDesempateDe(ProcessoSeletivo processo) =>
        (List<CriterioDesempate>)typeof(ProcessoSeletivo)
            .GetField("_criteriosDesempate", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(processo)!;

    [Fact(DisplayName = "Pré-canonicalização: área repetida entre critérios e classificação sem quadro — o checklist segue a precedência do gate")]
    public void PreCanon_DesempatePorArea_PrecedenciaDoChecklistSegueOGate()
    {
        ProcessoSeletivo processo = ProcessoConforme(); // classificação importada: sem quadro de pesos por área
        List<CriterioDesempate> criterios = (List<CriterioDesempate>)typeof(ProcessoSeletivo)
            .GetField("_criteriosDesempate", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(processo)!;
        criterios.AddRange(
            CriterioDesempate.Criar(1, Regra(CriterioDesempateCodigo.MaiorNotaAreaEnem, 'e'), new ArgsDesempateMaiorNotaAreaEnem(["REDACAO"])).Value!,
            CriterioDesempate.Criar(2, Regra(CriterioDesempateCodigo.MaiorNotaAreaEnem, 'e'), new ArgsDesempateMaiorNotaAreaEnem(["MATEMATICA", "REDACAO"])).Value!);

        string[] vermelhos = [.. processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario).Where(static i => !i.Ok).Select(static i => i.Codigo)];

        // Equal, não BeEquivalentTo: aqui a ORDEM é o que está sendo provado.
        vermelhos.Should().Equal(
            ["desempate_area_enem_citada_por_dois_criterios", "desempate_area_enem_sem_quadro"],
            "o gate recusa primeiro a área repetida, e o checklist reproduz a precedência de Publicar()");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.AreaEnemCitadaPorOutroCriterio");
    }

    [Fact(DisplayName = "Pré-canonicalização: área citada por dois critérios de desempate — só o item da área repetida fica vermelho, e Publicar recusa")]
    public void PreCanon_DesempatePorAreaCitadaPorDoisCriterios()
    {
        ProcessoSeletivo processo = ProcessoConformeComEtapaQueCompoeNota();
        typeof(ProcessoSeletivo).GetProperty(nameof(ProcessoSeletivo.Classificacao))!.SetValue(processo, ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.FormulaMediaPonderada, 'a'), Regra(RegraArredondamentoCodigo.PrecisaoTruncar, 'b'), 2,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, 'c'), 1, [], baseadoEmEnem: true,
            resolucaoPesoAreaEnem: QuadroPesoAreaEnemDeTeste.Resolucao, quadroPesoAreaEnem: QuadroPesoAreaEnemDeTeste.Completo()).Value!);
        // Direto na lista: a gravação recusa a área repetida, e o gate cobre o estado que
        // chega por outro caminho.
        List<CriterioDesempate> criterios = (List<CriterioDesempate>)typeof(ProcessoSeletivo)
            .GetField("_criteriosDesempate", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(processo)!;
        criterios.AddRange(
            CriterioDesempate.Criar(1, Regra(CriterioDesempateCodigo.MaiorNotaAreaEnem, 'e'), new ArgsDesempateMaiorNotaAreaEnem(["REDACAO"])).Value!,
            CriterioDesempate.Criar(2, Regra(CriterioDesempateCodigo.MaiorNotaAreaEnem, 'e'), new ArgsDesempateMaiorNotaAreaEnem(["MATEMATICA", "REDACAO"])).Value!);

        SoEstesItensVermelhos(processo, "desempate_area_enem_citada_por_dois_criterios");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.AreaEnemCitadaPorOutroCriterio");
        resultado.Error.Message.Should().Contain("REDACAO").And.Contain("ordem 1").And.NotContain("Áreas aceitas:");
    }

    [Fact(DisplayName = "Pré-canonicalização: desempate por área que cita área fora do quadro — só o item das áreas fica vermelho, e Publicar recusa")]
    public void PreCanon_DesempatePorAreaForaDoQuadro()
    {
        ProcessoSeletivo processo = ProcessoConformeComEtapaQueCompoeNota();
        PropertyInfo propriedadeClassificacao = typeof(ProcessoSeletivo).GetProperty(nameof(ProcessoSeletivo.Classificacao))!;
        propriedadeClassificacao.SetValue(processo, null);
        processo.DefinirCriteriosDesempate(
            [CriterioDesempate.Criar(1, Regra(CriterioDesempateCodigo.MaiorNotaAreaEnem, 'e'), new ArgsDesempateMaiorNotaAreaEnem(["FISICA"])).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        propriedadeClassificacao.SetValue(processo, ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.FormulaMediaPonderada, 'a'), Regra(RegraArredondamentoCodigo.PrecisaoTruncar, 'b'), 2,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, 'c'), 1, [], baseadoEmEnem: true,
            resolucaoPesoAreaEnem: QuadroPesoAreaEnemDeTeste.Resolucao, quadroPesoAreaEnem: QuadroPesoAreaEnemDeTeste.Completo()).Value!);

        SoEstesItensVermelhos(processo, "desempate_area_enem_fora_do_quadro");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.DesempateAreaEnemForaDoQuadro");
        resultado.Error.Message.Should().Contain("FISICA").And.Contain("Áreas aceitas:");
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // Precedência: coletar todos os vereditos não pode mudar qual DomainError vem primeiro
    // ══════════════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "Precedência preservada: com Gate 1 (itens estruturais) e Gate 2 (cronograma) pendentes ao mesmo tempo, Publicar devolve ConformidadeInsuficiente primeiro — mas o checklist mostra os DOIS itens vermelhos")]
    public void Precedencia_GatesSimultaneos_PublicarDevolveOPrimeiroNaOrdem()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Precedência", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar(
                "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        // OfertaAtendimento NUNCA definida — Gate 1 (PendenciaDeConformidade) fica pendente.
        processo.DefinirDistribuicaoVagas([DistribuicaoAmpla(10)], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirClassificacao(ClassificacaoImportada(), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        // Fase sem coleta de inscrição, com InscricaoPropria — Gate 2 (cronograma) TAMBÉM pendente.
        processo.DefinirCronogramaFases([FaseBase(coletaInscricao: false)], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        IReadOnlyList<ItemConformidade> checklist = processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario);
        checklist.Should().Contain(i => i.Codigo == "atendimento_especializado_ausente" && !i.Ok,
            "Gate 1 também está pendente — o checklist projeta TODOS os vereditos, não só o primeiro");
        checklist.Should().Contain(i => i.Codigo == "cronograma_inscricao_propria_sem_fase_de_coleta" && !i.Ok);

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(
            "ProcessoSeletivo.ConformidadeInsuficiente",
            "PendenciaDeConformidade é checado ANTES de PendenciaDoCronograma em Publicar (e em SucederVersao) — " +
            "coletar todos os vereditos para o checklist não pode mudar essa ordem");
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // Contraprovas — estados VÁLIDOS que não podem virar falso vermelho
    // ══════════════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "Contraprova: processo estruturalmente conforme tem o checklist inteiramente verde E Publicar aceita (cronograma coerente, cascata não aplicável, sem gatilho etário)")]
    public void Contraprova_ProcessoConforme_ChecklistVerdeEPublica()
    {
        ProcessoSeletivo processo = ProcessoConforme();

        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario).Should().OnlyContain(i => i.Ok);

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Contraprova: SiSU com CLASSIFICACAO-IMPORTADA e SEM etapa é estado válido — os dois itens de coerência etapa×fase ficam Ok (Story #851 §3.5)")]
    public void Contraprova_SiSU_ClassificacaoImportadaSemEtapa_ChecklistVerde()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        // ProcessoConforme já não define etapas nem fase que agrupa etapas — SiSU/importação
        // publica sem prova local. Esta contraprova torna essa premissa EXPLÍCITA.
        processo.Etapas.Should().BeEmpty("pré-condição da contraprova: sem etapa, sob CLASSIFICACAO-IMPORTADA");

        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario).Should().Contain(
            i => i.Codigo == "cronograma_fase_agrupadora_sem_etapa_pontuada" && i.Ok);
        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario).Should().Contain(
            i => i.Codigo == "cronograma_etapa_pontuada_sem_fase_agrupadora" && i.Ok);

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Contraprova: cascata não aplicável (nenhuma modalidade SegueCascata) mantém os cinco itens de cascata Ok")]
    public void Contraprova_CascataNaoAplicavel_ChecklistVerde()
    {
        ProcessoSeletivo processo = ProcessoConforme(); // modalidade única AC, RegraRemanejamentoModalidade.Nenhuma

        string[] itensDeCascata =
        [
            "cascata_modalidade_fora_do_regime_federal",
            "cascata_origem_ausente",
            "cascata_fallback_nao_ofertado",
            "cascata_origem_nao_segue_cascata",
            "cascata_destino_desconhecido",
        ];
        IReadOnlyList<ItemConformidade> checklist = processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario);
        foreach (string item in itensDeCascata)
        {
            checklist.Should().Contain(i => i.Codigo == item && i.Ok, $"cascata não se aplica — \"{item}\" não pode ficar vermelho");
        }
    }

    [Fact(DisplayName = "Contraprova: fato coletável CONDICAO_ATENDIMENTO com condição ofertada mantém o item Ok")]
    public void Contraprova_FatoColetadoCondicaoAtendimentoComOferta_ChecklistVerde()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        processo.DefinirOfertaAtendimento(OfertaComCondicaoPcd(), PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        FatoColetado fato = FatoColetado.Criar(
            "CONDICAO_ATENDIMENTO", 0, "Você se enquadra em alguma condição de atendimento?",
            TipoRenderizacao.SelecaoMultipla, obrigatorio: false, precondicoes: null).Value!;
        processo.DefinirFatosColetados([fato], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario).Should().Contain(
            i => i.Codigo == "fato_coletavel_sem_valores_ofertados" && i.Ok);

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Contraprova: fato coletável renderizado como Booleano não aciona o gate, mesmo sem oferta correspondente")]
    public void Contraprova_FatoColetadoBooleano_NaoAcionaGate()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        // Mesmo código de fato do gate, mas TipoRenderizacao fora de {SelecaoUnica, SelecaoMultipla}
        // — o gate só se aplica a campo com opções, que é o que fica vazio sem oferta.
        FatoColetado fato = FatoColetado.Criar(
            "CONDICAO_ATENDIMENTO", 0, "Possui alguma condição de atendimento?",
            TipoRenderizacao.Booleano, obrigatorio: false, precondicoes: null).Value!;
        processo.DefinirFatosColetados([fato], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario).Should().Contain(
            i => i.Codigo == "fato_coletavel_sem_valores_ofertados" && i.Ok);

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Contraprova: ausência de gatilho por FAIXA_ETARIA mantém os quatro itens de referência temporal Ok, mesmo com outras exigências configuradas")]
    public void Contraprova_SemGatilhoPorFaixaEtaria_ChecklistVerde()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        Guid faseId = processo.CronogramaFases.Single().Id;
        // Exigência GERAL, sem qualquer condição de gatilho — não aciona FAIXA_ETARIA.
        DocumentoExigido exigencia = DocumentoExigido.Criar(
            faseId, Guid.CreateVersion7(), "IDENTIDADE", "Documento de identidade", "PESSOAL",
            Aplicabilidade.Geral, obrigatorio: true, consequenciaIndeferimento: null,
            condicoes: [], basesLegais: [BaseLegalResolvida()], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(exigencia, 0).Value!], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        string[] itensDeReferenciaTemporal =
        [
            "referencia_temporal_ausente_com_gatilho_etario",
            "referencia_temporal_fase_fora_do_cronograma",
            "referencia_temporal_extremo_da_fase_ausente",
            "referencia_temporal_fim_inscricao_indisponivel",
        ];
        IReadOnlyList<ItemConformidade> checklist = processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario);
        foreach (string item in itensDeReferenciaTemporal)
        {
            checklist.Should().Contain(i => i.Codigo == item && i.Ok, $"sem gatilho por FAIXA_ETARIA — \"{item}\" não pode ficar vermelho");
        }

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // Gates que a raiz aplica antes do agregador — localidade e convenção de contagem
    // ══════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Calendário vigente mínimo — um feriado nacional basta para o gate passar. Existe para que
    /// um teste sobre a convenção de contagem isole a convenção: sem ele, o processo com recurso
    /// teria duas pendências, e a asserção não distinguiria qual das duas o teste prova.
    /// </summary>
    private static ContextoDeContagemDePrazos ComCalendario() => new(
        CalendarioDiasUteisCongelado.Criar(
            Guid.CreateVersion7(),
            "2026",
            [DiaNaoUtilCongelado.Criar(new DateOnly(2026, 1, 1), "NACIONAL", null, null, null).Value!]).Value,
        FusoInstitucional: TimeZoneInfo.FindSystemTimeZoneById(FusoInstitucional.ZoneId));

    private static ReferenciaRegra RegraDeRecursoAncorada() =>
        ReferenciaRegra.Criar(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('d', 64)).Value!;

    /// <summary>Produto preliminar de <see cref="FaseComRecurso"/> — a âncora do prazo.</summary>
    private static readonly Guid ProdutoPreliminarId = new("77770000-0000-4000-8000-000000000004");

    private static RegraRecursoFase RecursoEmHoras() => RegraRecursoFase.Criar(
        RegraDeRecursoAncorada(),
        new ArgsRegraPrazoRecurso(
            PrazoValor: 48m,
            PrazoUnidade: UnidadePrazo.Horas,
            SuspensividadePrimeiraInstanciaValor: null,
            SuspensividadePrimeiraInstanciaUnidade: null,
            SuspensividadeSegundaInstanciaValor: null,
            SuspensividadeSegundaInstanciaUnidade: null),
        ProdutoPreliminarId).Value!;

    /// <summary>Fase preliminar que publica o ato âncora e aceita recurso sobre ele.</summary>
    private static FaseCronograma FaseComRecurso() => FaseCronograma.Criar(
        1, Guid.CreateVersion7(), "RESULTADO_PRELIMINAR", "CEPS", OrigemDataFase.Delegada,
        agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false, inicio: null, fim: null,
        produtos: [ProdutoDaFase.Reidratar(ProdutoPreliminarId, "RESULTADO_PRELIMINAR", PapelProdutoFase.Preliminar)],
        faseConcluinteCodigo: "RESULTADO_FINAL",
        emiteParecerIndividual: false,
        bancasRequeridas: [], regraRecurso: RecursoEmHoras()).Value!;

    private static FaseCronograma FaseFinal(int ordem) => FaseCronograma.Criar(
        ordem, Guid.CreateVersion7(), "RESULTADO_FINAL", "CEPS", OrigemDataFase.Delegada,
        agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false, inicio: null, fim: null,
        produtos: [ProdutoDaFase.Criar("RESULTADO_FINAL", PapelProdutoFase.Definitivo)],
        faseConcluinteCodigo: null,
        emiteParecerIndividual: false,
        bancasRequeridas: [], regraRecurso: null).Value!;

    [Fact(DisplayName = "Convenção de contagem não declarada com fase que aceita recurso — item vermelho e Publicar recusa com AlgoritmoContagemPrazoNaoDeclarado")]
    public void ContagemDePrazos_AlgoritmoNaoDeclarado()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        processo.DefinirCronogramaFases([FaseComRecurso(), FaseFinal(2)], [], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        SoEstesItensVermelhos(processo, ComCalendario(), "algoritmo_contagem_prazo_nao_declarado");

        Result<VersaoConfiguracao> resultado = Publicar(processo, ComCalendario());
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.AlgoritmoContagemPrazoNaoDeclarado");
    }

    [Fact(DisplayName = "Localidade ausente — item vermelho e Publicar recusa com LocalidadeAusente")]
    public void ContagemDePrazos_LocalidadeAusente()
    {
        ProcessoSeletivo processo = ProcessoConforme();

        // A localidade é exigida desde a criação, então o fluxo público não alcança este
        // estado — o que se prova aqui é a defesa da raiz contra um caminho de escrita que a
        // contorne, incluindo a materialização de um registro gravado antes de o campo existir.
        // Sem essa prova, a projeção do item ficaria verde por construção e nunca exercitada.
        typeof(ProcessoSeletivo).GetProperty(nameof(ProcessoSeletivo.Localidade))!
            .SetValue(processo, null);

        SoEstesItensVermelhos(processo, "localidade_nao_declarada");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.LocalidadeAusente");
    }

    [Fact(DisplayName = "Com duas pendências simultâneas, o checklist lista a que bloqueia a publicação antes da outra")]
    public void ContagemDePrazos_PrecedenciaDoChecklistSegueOsGates()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        // Duas pendências ao mesmo tempo, de gates que Publicar() avalia em ordem conhecida:
        // a convenção de contagem (2º) e a taxa de inscrição, que é item estrutural e cai no
        // agregador genérico (3º). Quem lê o checklist de cima para baixo tem de encontrar a
        // causa que efetivamente bloqueia antes da que só apareceria depois dela.
        processo.DefinirCronogramaFases([FaseComRecurso(), FaseFinal(2)], [], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();
        processo.DefinirTaxaInscricao(null!, PrecondicaoIfMatch.Curinga);

        string[] vermelhos = [.. processo.AvaliarConformidade(ComCalendario()).Where(static i => !i.Ok).Select(static i => i.Codigo)];

        // Equal, não BeEquivalentTo: aqui a ORDEM é o que está sendo provado.
        vermelhos.Should().Equal(
            ["algoritmo_contagem_prazo_nao_declarado", "taxa_inscricao_nao_declarada"],
            "a convenção de contagem é gate anterior ao agregador de itens estruturais, e o " +
            "checklist reproduz a precedência de Publicar()");

        Result<VersaoConfiguracao> resultado = Publicar(processo, ComCalendario());
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.AlgoritmoContagemPrazoNaoDeclarado",
            "a publicação recusa pela primeira causa da mesma precedência");
    }

    [Fact(DisplayName = "Contraprova: sem fase que aceite recurso, a convenção de contagem não é exigida")]
    public void ContagemDePrazos_SemRecursoNaoExigeConvencao()
    {
        ProcessoSeletivo processo = ProcessoConforme();

        SoEstesItensVermelhos(processo);

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // Processo baseado em ENEM com cálculo local: resolução, quadro e grupo das ofertas
    // ══════════════════════════════════════════════════════════════════════════════════════

    private static readonly (string? Codigo, string? Rotulo) GrupoHumanisticaI = ("HUMANISTICA_I", "Humanística I");

    private static ConfiguracaoClassificacao ClassificacaoEnemLocal(IReadOnlyList<GrupoPesoAreaEnemCongelado> quadro) =>
        ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.FormulaMediaPonderada, 'a'), Regra(RegraArredondamentoCodigo.PrecisaoTruncar, 'b'), 2,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, 'c'), 1, [], baseadoEmEnem: true,
            resolucaoPesoAreaEnem: QuadroPesoAreaEnemDeTeste.Resolucao, quadroPesoAreaEnem: quadro).Value!;

    private static ProcessoSeletivo ProcessoEnemLocal(
        IReadOnlyList<ConfiguracaoDistribuicaoVagas> ofertas,
        IReadOnlyList<GrupoPesoAreaEnemCongelado>? quadro = null)
    {
        ProcessoSeletivo processo = ProcessoConformeComEtapaQueCompoeNota();
        processo.DefinirDistribuicaoVagas(ofertas, PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        processo.DefinirClassificacao(ClassificacaoEnemLocal(quadro ?? QuadroPesoAreaEnemDeTeste.Completo()), PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();
        return processo;
    }

    /// <summary>A fábrica recusa quadro vazio; o EF hidrata a linha sem passar por ela.</summary>
    private static void EsvaziarQuadro(ConfiguracaoClassificacao classificacao) =>
        ((List<GrupoPesoAreaEnemCongelado>)typeof(ConfiguracaoClassificacao)
            .GetField("_quadroPesoAreaEnem", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(classificacao)!).Clear();

    private static ItemConformidade Item(ProcessoSeletivo processo, string codigo) =>
        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario).Single(i => i.Codigo == codigo);

    [Fact(DisplayName = "ENEM com cálculo local: resolução, quadro e grupo do quadro em toda oferta — os itens existem, nenhum vermelho, e Publicar conclui")]
    public void Enem_ProcessoCompletoPublica()
    {
        ProcessoSeletivo processo = ProcessoEnemLocal([OfertaAmpla(10, GrupoTecnologica), OfertaAmpla(5, GrupoHumanisticaI)]);

        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario).Select(static i => i.Codigo).Should().Contain(
            ["classificacao_resolucao_peso_area_enem_ausente", "distribuicao_vagas_oferta_sem_grupo_area_enem", "classificacao_grupo_area_enem_da_oferta_fora_do_quadro", "desempate_area_enem_sem_quadro"],
            "a exigência existe, e o item existe com ela; o do desempate sem quadro some só quando a resolução está pendente");
        SoEstesItensVermelhos(processo);

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    public enum FaltaNaResolucao
    {
        ResolucaoAusente,
        ResolucaoEmBranco,
        QuadroVazio,
    }

    [Theory(DisplayName = "ENEM com cálculo local: resolução ausente ou em branco, ou quadro vazio, estado que só chega sem passar pela fábrica — só o item da resolução fica vermelho, e Publicar recusa")]
    [InlineData(FaltaNaResolucao.ResolucaoAusente)]
    [InlineData(FaltaNaResolucao.ResolucaoEmBranco)]
    [InlineData(FaltaNaResolucao.QuadroVazio)]
    public void Enem_ResolucaoOuQuadroAusente(FaltaNaResolucao falta)
    {
        ProcessoSeletivo processo = ProcessoEnemLocal([OfertaAmpla(10, GrupoTecnologica)]);
        PropertyInfo resolucao = typeof(ConfiguracaoClassificacao).GetProperty(nameof(ConfiguracaoClassificacao.ResolucaoPesoAreaEnem))!;

        switch (falta)
        {
            case FaltaNaResolucao.ResolucaoAusente:
                resolucao.SetValue(processo.Classificacao, null);
                break;
            case FaltaNaResolucao.ResolucaoEmBranco:
                resolucao.SetValue(processo.Classificacao, "   ");
                break;
            default:
                EsvaziarQuadro(processo.Classificacao!);
                break;
        }

        SoEstesItensVermelhos(processo, "classificacao_resolucao_peso_area_enem_ausente");
        Item(processo, "classificacao_resolucao_peso_area_enem_ausente").Dimensao.Should().Be(DimensaoConformidade.Classificacao);
        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario).Any(static i => i.Codigo == "desempate_area_enem_sem_quadro")
            .Should().Be(falta != FaltaNaResolucao.QuadroVazio, "o item do desempate sem quadro só sai do checklist quando é o quadro que falta");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.ConformidadeInsuficiente");
        resultado.Error.Message.Should().Contain("Resolução de Pesos por Área");
    }

    [Fact(DisplayName = "ENEM com cálculo local: ofertas sem grupo de área — só o item do grupo fica vermelho, nomeando as ofertas, e Publicar recusa")]
    public void Enem_OfertaSemGrupo()
    {
        ConfiguracaoDistribuicaoVagas comGrupo = OfertaAmpla(10, GrupoTecnologica);
        ConfiguracaoDistribuicaoVagas semGrupo = OfertaAmpla(5, grupoAreaEnem: null);
        ConfiguracaoDistribuicaoVagas outraSemGrupo = OfertaAmpla(3, grupoAreaEnem: null);
        ProcessoSeletivo processo = ProcessoEnemLocal([comGrupo, semGrupo, outraSemGrupo]);

        SoEstesItensVermelhos(processo, "distribuicao_vagas_oferta_sem_grupo_area_enem");
        ItemConformidade item = Item(processo, "distribuicao_vagas_oferta_sem_grupo_area_enem");
        item.Dimensao.Should().Be(DimensaoConformidade.DistribuicaoVagas);
        item.Mensagem.Should().Contain(semGrupo.OfertaCursoOrigemId.ToString())
            .And.Contain(outraSemGrupo.OfertaCursoOrigemId.ToString())
            .And.NotContain(comGrupo.OfertaCursoOrigemId.ToString());

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.ConformidadeInsuficiente");
        resultado.Error.Message.Should().Contain(outraSemGrupo.OfertaCursoOrigemId.ToString());
    }

    [Fact(DisplayName = "ENEM com cálculo local: com a retificação aberta, a distribuição regravada com oferta sem grupo faz o fechamento recusar pelo mesmo item")]
    public void Enem_FecharRetificacaoComOfertaSemGrupo_Recusa()
    {
        ProcessoSeletivo processo = ProcessoEnemLocal([OfertaAmpla(10, GrupoTecnologica)]);
        Result<VersaoConfiguracao> publicada = Publicar(processo);
        publicada.IsSuccess.Should().BeTrue(publicada.Error?.Message);

        // A retificação aberta libera a gravação da distribuição; regravá-la depois de o curso
        // perder o grupo no cadastro congela a oferta sem grupo.
        processo.AbrirRetificacao("Ajustar as vagas", publicada.Value!, "teste", DateTimeOffset.UnixEpoch)
            .IsSuccess.Should().BeTrue();
        ConfiguracaoDistribuicaoVagas semGrupo = OfertaAmpla(12, grupoAreaEnem: null);
        processo.DefinirDistribuicaoVagas([semGrupo], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        Result<VersaoConfiguracao> fechar = processo.FecharRetificacao(
            Dados(), publicada.Value!, "{}"u8.ToArray(), "1.1", "canonical-json/sha256@v1", HashFixo, "teste",
            PrecondicaoIfMatch.Curinga, TimeProvider.System, ContextoDeContagemDePrazos.SemCalendario);

        fechar.IsFailure.Should().BeTrue();
        fechar.Error!.Code.Should().Be("ProcessoSeletivo.ConformidadeInsuficiente");
        fechar.Error.Message.Should().Contain(semGrupo.OfertaCursoOrigemId.ToString());
    }

    [Fact(DisplayName = "ENEM com cálculo local: a recusa de Publicar fica bem pontuada com um item e com vários")]
    public void Enem_MensagemAgregadaBemPontuada()
    {
        ConfiguracaoDistribuicaoVagas[] semGrupo = [.. new[] { OfertaAmpla(5, grupoAreaEnem: null), OfertaAmpla(4, grupoAreaEnem: null) }
            .OrderBy(static o => o.OfertaCursoOrigemId)];
        string umItem = Publicar(ProcessoEnemLocal(semGrupo)).Error!.Message;
        DeveEstarBemPontuada(umItem);
        umItem.Should().Contain($"(ofertas: {semGrupo[0].OfertaCursoOrigemId}; {semGrupo[1].OfertaCursoOrigemId})",
            "os Ids se separam por ponto e vírgula, e o agregador separa os itens por vírgula");

        ConfiguracaoDistribuicaoVagas outraSemGrupo = OfertaAmpla(5, grupoAreaEnem: null);
        ConfiguracaoDistribuicaoVagas foraDoQuadro = OfertaAmpla(10, GrupoTecnologica);
        string doisItens = Publicar(ProcessoEnemLocal(
            [outraSemGrupo, foraDoQuadro], [QuadroPesoAreaEnemDeTeste.Grupo("HUMANISTICA_I", "Humanística I")])).Error!.Message;
        DeveEstarBemPontuada(doisItens);
        doisItens.Should().Contain(outraSemGrupo.OfertaCursoOrigemId.ToString())
            .And.Contain($"{foraDoQuadro.OfertaCursoOrigemId} (grupo TECNOLOGICA — Tecnológica))");
    }

    /// <summary>A redação pode mudar; a pontuação da mensagem agregada, não.</summary>
    private static void DeveEstarBemPontuada(string mensagem)
    {
        mensagem.Should().NotContain("..").And.NotContain("., ").And.NotContain(";)").And.EndWith(").");
    }

    [Fact(DisplayName = "ENEM com cálculo local: as ofertas pendentes aparecem na mensagem em ordem estável, qualquer que seja a ordem da distribuição")]
    public void Enem_OfertasEmOrdemEstavel()
    {
        ConfiguracaoDistribuicaoVagas[] ofertas = [OfertaAmpla(5, grupoAreaEnem: null), OfertaAmpla(3, grupoAreaEnem: null)];
        ConfiguracaoDistribuicaoVagas[] emOrdem = [.. ofertas.OrderBy(static o => o.OfertaCursoOrigemId)];
        ProcessoSeletivo processo = ProcessoEnemLocal([emOrdem[1], emOrdem[0]]);

        string mensagem = Item(processo, "distribuicao_vagas_oferta_sem_grupo_area_enem").Mensagem;
        mensagem.IndexOf(emOrdem[0].OfertaCursoOrigemId.ToString(), StringComparison.Ordinal).Should()
            .BeLessThan(mensagem.IndexOf(emOrdem[1].OfertaCursoOrigemId.ToString(), StringComparison.Ordinal));
    }

    [Fact(DisplayName = "ENEM com cálculo local: oferta com grupo fora do quadro — só o item do quadro fica vermelho, na classificação, nomeando a oferta e o grupo por código e rótulo, e Publicar recusa")]
    public void Enem_GrupoForaDoQuadro()
    {
        ConfiguracaoDistribuicaoVagas noQuadro = OfertaAmpla(5, GrupoHumanisticaI);
        ConfiguracaoDistribuicaoVagas foraDoQuadro = OfertaAmpla(10, GrupoTecnologica);
        ProcessoSeletivo processo = ProcessoEnemLocal(
            [noQuadro, foraDoQuadro], [QuadroPesoAreaEnemDeTeste.Grupo("HUMANISTICA_I", "Humanística I")]);

        SoEstesItensVermelhos(processo, "classificacao_grupo_area_enem_da_oferta_fora_do_quadro");
        ItemConformidade item = Item(processo, "classificacao_grupo_area_enem_da_oferta_fora_do_quadro");
        item.Dimensao.Should().Be(DimensaoConformidade.Classificacao);
        item.Mensagem.Should()
            .Contain($"{foraDoQuadro.OfertaCursoOrigemId} (grupo TECNOLOGICA — Tecnológica)")
            .And.NotContain(noQuadro.OfertaCursoOrigemId.ToString());

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.ConformidadeInsuficiente");
        resultado.Error.Message.Should().Contain(foraDoQuadro.OfertaCursoOrigemId.ToString());
    }

    [Fact(DisplayName = "ENEM com cálculo local: classificação sem quadro gravada sobre desempate por área recusa no campo da resolução, nunca em silêncio")]
    public void Enem_DefinirClassificacaoSemQuadroComDesempatePorArea_RecusaNaResolucao()
    {
        ProcessoSeletivo processo = ProcessoEnemLocal([OfertaAmpla(10, GrupoTecnologica)]);
        processo.DefinirCriteriosDesempate(
            [CriterioDesempate.Criar(1, Regra(CriterioDesempateCodigo.MaiorNotaAreaEnem, 'e'), new ArgsDesempateMaiorNotaAreaEnem(["REDACAO"])).Value!],
            PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        ConfiguracaoClassificacao semQuadro = ClassificacaoEnemLocal(QuadroPesoAreaEnemDeTeste.Completo());
        EsvaziarQuadro(semQuadro);

        Result resultado = processo.DefinirClassificacao(semQuadro, PrecondicaoIfMatch.Curinga);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            (ConfiguracaoClassificacao.CampoResolucaoPesoAreaEnem, "ProcessoSeletivo.DesempateAreaEnemSemQuadro"));
    }

    [Fact(DisplayName = "ENEM com cálculo local: o rótulo do grupo volta na mensagem limitado ao teto de eco")]
    public void Enem_GrupoForaDoQuadro_RotuloLimitadoAoTetoDeEco()
    {
        string rotuloLongo = new('x', GrupoAreaEnemSnapshot.RotuloMaxLength);
        ProcessoSeletivo processo = ProcessoEnemLocal(
            [OfertaAmpla(10, ("OUTRO", rotuloLongo))], [QuadroPesoAreaEnemDeTeste.Grupo("HUMANISTICA_I", "Humanística I")]);

        Item(processo, "classificacao_grupo_area_enem_da_oferta_fora_do_quadro").Mensagem.Should()
            .Contain($"(grupo OUTRO — {new string('x', 40)}…)",
                "o rótulo volta cortado no teto de eco das mensagens de recusa, de 40 caracteres");
    }

    [Fact(DisplayName = "ENEM com cálculo local: sem quadro, nem a oferta com grupo nem o desempate por área abrem um segundo vermelho pela mesma causa")]
    public void Enem_SemQuadroNaoDuplicaPendencia()
    {
        ProcessoSeletivo processo = ProcessoEnemLocal([OfertaAmpla(5, GrupoHumanisticaI)]);
        processo.DefinirCriteriosDesempate(
            [CriterioDesempate.Criar(1, Regra(CriterioDesempateCodigo.MaiorNotaAreaEnem, 'e'), new ArgsDesempateMaiorNotaAreaEnem(["REDACAO"])).Value!],
            PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        EsvaziarQuadro(processo.Classificacao!);

        SoEstesItensVermelhos(processo, "classificacao_resolucao_peso_area_enem_ausente");
        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario).Select(static i => i.Codigo).Should().NotContain(
            "desempate_area_enem_sem_quadro", "sairia verde com a descrição de que há quadro, justamente quando ele falta");
        processo.PendenciaPreCanonicalizacao().Should().BeNull(
            "o gate diz o mesmo que o checklist: a falta do quadro é só pendência da resolução");

        // A deduplicação é da conformidade: a gravação do desempate continua recusando.
        Result gravar = processo.DefinirCriteriosDesempate(
            [CriterioDesempate.Criar(1, Regra(CriterioDesempateCodigo.MaiorNotaAreaEnem, 'e'), new ArgsDesempateMaiorNotaAreaEnem(["REDACAO"])).Value!],
            PrecondicaoIfMatch.Curinga);
        gravar.IsFailure.Should().BeTrue();
        gravar.Error!.Code.Should().Be("ProcessoSeletivo.DesempateAreaEnemSemQuadro");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.ConformidadeInsuficiente");
    }

    [Fact(DisplayName = "Contraprova: cálculo local que não é baseado em ENEM não traz os itens da média pelos pesos por área, dispensa grupo de área na oferta, e Publicar conclui")]
    public void Enem_CalculoLocalSemEnemDispensaGrupo()
    {
        ProcessoSeletivo processo = ProcessoConformeComEtapaQueCompoeNota();
        processo.DefinirDistribuicaoVagas([OfertaAmpla(10, grupoAreaEnem: null)], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        processo.DefinirClassificacao(
            ConfiguracaoClassificacao.Criar(
                Regra(RegraCalculoCodigo.FormulaMediaPonderada, 'a'), Regra(RegraArredondamentoCodigo.PrecisaoTruncar, 'b'), 2,
                Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, 'c'), 1, [], baseadoEmEnem: false,
                resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: []).Value!,
            PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario).Select(static i => i.Codigo).Should().NotContain(
            ["classificacao_resolucao_peso_area_enem_ausente", "distribuicao_vagas_oferta_sem_grupo_area_enem", "classificacao_grupo_area_enem_da_oferta_fora_do_quadro"]);
        SoEstesItensVermelhos(processo);

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Contraprova: classificação baseada em ENEM importada não traz os itens da média local, dispensa grupo de área na oferta, e Publicar conclui")]
    public void Enem_ClassificacaoImportadaDispensaGrupo()
    {
        ProcessoSeletivo processo = ProcessoConforme([OfertaAmpla(10, grupoAreaEnem: null)]);
        processo.DefinirClassificacao(
            ConfiguracaoClassificacao.Criar(
                Regra(RegraCalculoCodigo.ClassificacaoImportada, 'b'), null, null,
                Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, 'c'), 1, [], baseadoEmEnem: true,
                resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: []).Value!,
            PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario).Select(static i => i.Codigo).Should().NotContain(
            ["classificacao_resolucao_peso_area_enem_ausente", "distribuicao_vagas_oferta_sem_grupo_area_enem", "classificacao_grupo_area_enem_da_oferta_fora_do_quadro"]);
        SoEstesItensVermelhos(processo);

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }
}
