namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Reflection;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Opcoes;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.Services;
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
public sealed partial class ConformidadePublicabilidadeEstruturalTests
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
        Dados(), "{}"u8.ToArray(), "1.1", "canonical-json/sha256@v1", HashFixo, "teste", TimeProvider.System, contexto, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);

    private static ConfiguracaoClassificacao ClassificacaoImportada() => ConfiguracaoClassificacao.Criar(
        Regra(RegraCalculoCodigo.ClassificacaoImportada, 'b'), null, null,
        Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'), [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: [], escalaNotaFinal: null).Value!;

    private static ModalidadeSelecionada Modalidade(
        string codigo,
        NaturezaLegalModalidade natureza,
        RegraRemanejamentoModalidade remanejamento,
        int? quantidadeDeclarada,
        string? acaoQuandoIndeferido = null,
        PredicadoDnf? regra = null) =>
        ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), codigo, null, natureza,
            natureza == NaturezaLegalModalidade.Ampla ? ComposicaoVagasModalidade.ResidualDoVo : ComposicaoVagasModalidade.DentroDoVr,
            null, remanejamento, null, null, null, regra ?? RegrasDeAplicabilidadeDeTeste.DaNatureza(natureza), acaoQuandoIndeferido, "base legal", quantidadeDeclarada).Value!;

    private static readonly (string? Codigo, string? Rotulo) GrupoTecnologica = ("TECNOLOGICA", "Tecnológica");

    private static ConfiguracaoDistribuicaoVagas DistribuicaoAmpla(int voBase, string? acaoQuandoIndeferido = null) =>
        OfertaAmpla(voBase, grupoAreaEnem: null, acaoQuandoIndeferido);

    private static ConfiguracaoDistribuicaoVagas OfertaAmpla(
        int voBase, (string? Codigo, string? Rotulo)? grupoAreaEnem, string? acaoQuandoIndeferido = null) =>
        ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase, pr: 1m,
            Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'), regraAjuste: null, referenciaDemografica: null,
            [Modalidade("AC", NaturezaLegalModalidade.Ampla, RegraRemanejamentoModalidade.Nenhuma, voBase, acaoQuandoIndeferido)],
            grupoAreaEnem: grupoAreaEnem, ofertaCurso: OfertaCursoDeTeste.Medicina).Value!;

    /// <summary>
    /// Os itens vermelhos do checklist, sem os do formulário: os testes que isolam uma razão de
    /// recusa montam processos sem formulário, ou com o formulário mínimo sem a fase de inscrição, e
    /// os itens do formulário ficariam vermelhos por isso — legitimamente, mas fora do que o teste
    /// isola.
    /// </summary>
    private static IEnumerable<ItemConformidade> VermelhosForaDoFormulario(ProcessoSeletivo processo) =>
        ChecklistDeConformidade.AvaliarConformidade(EstadoDaConfiguracao.De(processo), ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum)
            .Where(static i => !i.Ok && !i.Codigo.StartsWith("formulario_", StringComparison.Ordinal));

    /// <summary>Fase mínima e coerente: não agrupa etapas (dispensável sem prova), produz resultado, não coleta inscrição.</summary>
    private static FaseCronograma FaseBase(
        bool coletaInscricao = false, bool coletaSolicitacaoIsencao = false, Guid? faseCanonicaOrigemId = null,
        bool semUnidadeResponsavel = false) => FaseCronograma.Criar(
        1, faseCanonicaOrigemId ?? Guid.CreateVersion7(), "RESULTADO_FINAL", OrigemDataFase.NaExecucao,
        agrupaEtapas: false, permiteComplementacao: false, coletaInscricao, coletaSolicitacaoIsencao, inicio: null, fim: null,
        produtos: [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)],
        faseConcluinteCodigo: null,
        emiteParecerIndividual: false,
        bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: semUnidadeResponsavel ? null : UnidadesDeTeste.Ceps).Value!;

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
                "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());

        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirDistribuicaoVagas(distribuicao ?? [DistribuicaoAmpla(10)], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente)
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
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);

        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);
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
        processo.DefinirCronogramaFases(
            [FaseCronograma.Criar(
                1, Guid.CreateVersion7(), "RESULTADO_FINAL", OrigemDataFase.NaExecucao,
                agrupaEtapas: true, permiteComplementacao: false,                 coletaInscricao: false, coletaSolicitacaoIsencao: false, inicio: null, fim: null,
                produtos: [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)],
                faseConcluinteCodigo: null,
                emiteParecerIndividual: false,
                bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps).Value!],
            [], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        processo.DefinirEtapas(
            [EtapaProcesso.Criar("Prova", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, peso: 1m, ordem: 1, notaMaxima: 10m, formaDeAvaliacao: FormaDeAvaliacao.Objetiva, faseCodigo: "RESULTADO_FINAL").Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();
        processo.DefinirEtapas([], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        ChecklistDeConformidade.AvaliarConformidade(EstadoDaConfiguracao.De(processo), ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum).Should().ContainSingle(i => !i.Ok)
            .Which.Codigo.Should().Be("cronograma_fase_agrupadora_sem_etapa_pontuada");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.AvaliacaoSemEtapa");
    }

    [Fact(DisplayName = "Cronograma: etapa pontuada sem fase declarada — item vermelho e Publicar recusa com EtapaSemFaseDeclarada")]
    public void Cronograma_EtapaSemFaseDeclarada()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        processo.DefinirEtapas(
            [EtapaProcesso.Criar("Prova", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, peso: 1m, ordem: 1, notaMaxima: 10m, formaDeAvaliacao: FormaDeAvaliacao.Objetiva).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        ChecklistDeConformidade.AvaliarConformidade(EstadoDaConfiguracao.De(processo), ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum).Should().ContainSingle(i => !i.Ok)
            .Which.Codigo.Should().Be("cronograma_etapa_declara_a_fase");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.EtapaSemFaseDeclarada");
    }

    [Fact(DisplayName = "Cronograma: InscricaoPropria sem fase de coleta — item vermelho e Publicar recusa com InscricaoPropriaSemFaseDeColeta (cenário da issue)")]
    public void Cronograma_InscricaoPropriaSemFaseDeColeta()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Matriz Estrutural", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar(
                "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirDistribuicaoVagas([DistribuicaoAmpla(10)], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirClassificacao(ClassificacaoImportada(), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirCronogramaFases([FaseBase(coletaInscricao: false)], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);

        VermelhosForaDoFormulario(processo).Should().ContainSingle()
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
                "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirDistribuicaoVagas([DistribuicaoAmpla(10)], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirClassificacao(ClassificacaoImportada(), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        // A fase coleta inscrição e é NA_EXECUCAO, então a janela é opcional para o cadastro (§3.2)
        // — mas não para publicar: é dela que o Edital tira o prazo (issue #1350).
        processo.DefinirCronogramaFases([FaseBase(coletaInscricao: true)], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);

        VermelhosForaDoFormulario(processo).Should().ContainSingle()
            .Which.Codigo.Should().Be("cronograma_fase_que_coleta_inscricao_sem_janela");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.FaseQueColetaInscricaoSemJanela");
    }

    [Fact(DisplayName = "Cronograma: fase sem unidade responsável — item vermelho e Publicar recusa com FaseSemUnidadeResponsavel, citando a fase")]
    public void Cronograma_FaseSemUnidadeResponsavel()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        FaseCronograma habilitacao = FaseCronograma.Criar(
            2, Guid.CreateVersion7(), "HABILITACAO", OrigemDataFase.NaExecucao,
            agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false, inicio: null, fim: null,
            produtos: [], faseConcluinteCodigo: null, emiteParecerIndividual: false,
            bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: null).Value!;

        // O rascunho aceita a fase ainda sem unidade; só a publicação a exige.
        processo.DefinirCronogramaFases([FaseBase(), habilitacao], [], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        VermelhosForaDoFormulario(processo).Should().ContainSingle()
            .Which.Codigo.Should().Be("cronograma_fase_sem_unidade_responsavel");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.FaseSemUnidadeResponsavel");
        resultado.Error.Message.Should().Contain("HABILITACAO");
    }

    [Fact(DisplayName = "Cronograma: vagas ofertadas sem fase que produz resultado — item vermelho e Publicar recusa com VagasSemFaseQueProduzResultado")]
    public void Cronograma_VagasSemFaseQueProduzResultado()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        processo.DefinirCronogramaFases(
            [FaseCronograma.Criar(
                1, Guid.CreateVersion7(), "MATRICULA", OrigemDataFase.NaExecucao,
                agrupaEtapas: false, permiteComplementacao: false,                 coletaInscricao: false, coletaSolicitacaoIsencao: false, inicio: null, fim: null,
                produtos: [], faseConcluinteCodigo: null, emiteParecerIndividual: false,
                bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps).Value!],
            [], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        ChecklistDeConformidade.AvaliarConformidade(EstadoDaConfiguracao.De(processo), ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum).Should().ContainSingle(i => !i.Ok)
            .Which.Codigo.Should().Be("cronograma_vagas_sem_fase_que_produz_resultado");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.VagasSemFaseQueProduzResultado");
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // PendenciaDaCascata — cinco razões (cascata de remanejamento de cotas, UNI-REQ-0056)
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
        Guid.CreateVersion7(), voBase: 40, pr: 0.5m, RegraLei12711(), RegraAjusteArt11(), Demografica(), AsOitoFederaisMaisAc(), ofertaCurso: OfertaCursoDeTeste.Medicina).Value!;
}
