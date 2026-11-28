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
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioConformidadePublicabilidadeEstrutural;

/// <summary>
/// A matriz que pareia cada um dos códigos de recusa alcançáveis nos quatra conferências
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
    // ══════════════════════════════════════════════════════════════════════════════════════
    // PendenciaDoCronograma — quatro razões
    // ══════════════════════════════════════════════════════════════════════════════════════

    private static FaseCronograma FaseDeResultadoFinal() => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        OrigensDoTipoDeFaseDeTeste.Da(
            Guid.CreateVersion7(), "RESULTADO_FINAL", OrigemDataFase.NaExecucao, agrupaEtapas: true, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(1, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
        HomologacaoDasInscricoesDeTeste.PublicacoesDaFaseQueHomologaComRecurso(),
        bancasRequeridas: HomologacaoDasInscricoesDeTeste.BancasDaFaseQueHomologaComRecurso()).Value!;

    private static void DefinirEDepoisRemoverAEtapaPontuada(ProcessoSeletivo processo)
    {
        processo.DefinirEtapas(
            [EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
                new IdentificacaoDaEtapa(Nome: "Prova", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: "RESULTADO_FINAL"),
                new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!])
            .IsSuccess.Should().BeTrue();
        processo.DefinirEtapas([]).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Cronograma: fase de avaliação sem etapa pontuada — item vermelho e Publicar recusa com AvaliacaoSemEtapa")]
    public void Cronograma_FaseDeAvaliacaoSemEtapa()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        // DefinirCronogramaFases bloqueia EAGERLY uma fase que agrupa etapas sem etapa nenhuma —
        // por isso a etapa é definida ANTES (torna a fase agrupa-etapas válida na hora), e só
        // DEPOIS removida via DefinirEtapas([]): a fase de avaliação fica órfã, e essa direção só
        // a conferência de publicação (LAZY) pega — é exatamente o defeito que o docblock de
        // PendenciaDoCronograma descreve como "defesa em profundidade".
        FormularioDeTeste.TrocarCronograma(processo, [FaseDeResultadoFinal()]).IsSuccess.Should().BeTrue();
        DefinirEDepoisRemoverAEtapaPontuada(processo);

        SoEstesItensVermelhos(processo, "cronograma_fase_agrupadora_sem_etapa_pontuada");

        ExigirPublicacaoRecusada(processo, "ProcessoSeletivo.AvaliacaoSemEtapa");
    }

    [Fact(DisplayName = "Cronograma: etapa pontuada sem fase declarada — itens da etapa e da fase agrupadora vermelhos, e Publicar recusa com EtapaSemFaseDeclarada")]
    public void Cronograma_EtapaSemFaseDeclarada()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        processo.DefinirEtapas(
            [EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
                new IdentificacaoDaEtapa(Nome: "Prova", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: null),
                new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!])
            .IsSuccess.Should().BeTrue();

        // Sem saber em que fase a etapa acontece, não se sabe se a fase que agrupa etapas tem etapa nela: o item fica indeterminado, e vermelho.
        SoEstesItensVermelhos(processo, "cronograma_fase_agrupadora_sem_etapa_pontuada", "cronograma_etapa_declara_a_fase");

        ExigirPublicacaoRecusada(processo, "ProcessoSeletivo.EtapaSemFaseDeclarada");
    }

    private static ProcessoSeletivo NovoProcessoDeInscricaoPropriaSemCronograma()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Matriz Estrutural", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar(
                "CEPS",
                "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        processo.DefinirAtendimentoEspecializado(AtendimentoEspecializado.Criar([], [], []).Value!)
            .IsSuccess.Should().BeTrue();
        processo.DefinirDistribuicaoVagas([DistribuicaoAmpla(10)], FatosDeModalidadeDeTeste.DoCatalogo).IsSuccess.Should().BeTrue();
        processo.DefinirClassificacao(ClassificacaoImportada()).IsSuccess.Should().BeTrue();
        return processo;
    }

    [Fact(DisplayName = "Cronograma: InscricaoPropria sem fase de coleta — item vermelho e Publicar recusa com InscricaoPropriaSemFaseDeColeta")]
    public void Cronograma_InscricaoPropriaSemFaseDeColeta()
    {
        ProcessoSeletivo processo = NovoProcessoDeInscricaoPropriaSemCronograma();
        FormularioDeTeste.TrocarCronograma(processo, [FaseBase(coletaInscricao: false)]).IsSuccess.Should().BeTrue();
        DeclararTaxaSemCobranca(processo);

        VermelhosForaDoFormulario(processo).Should().ContainSingle()
            .Which.Codigo.Should().Be("cronograma_inscricao_propria_sem_fase_de_coleta");

        ExigirPublicacaoRecusada(processo, "ProcessoSeletivo.InscricaoPropriaSemFaseDeColeta");
    }

    [Fact(DisplayName = "Cronograma: fase que coleta inscrição sem janela — item vermelho e Publicar recusa com FaseQueColetaInscricaoSemJanela")]
    public void Cronograma_FaseQueColetaInscricaoSemJanela()
    {
        ProcessoSeletivo processo = NovoProcessoDeInscricaoPropriaSemCronograma();

        // A fase coleta inscrição e é NA_EXECUCAO, então a janela é opcional para o cadastro
        // — mas não para publicar: é dela que o Edital tira o prazo.
        FormularioDeTeste.TrocarCronograma(processo, [FaseBase(coletaInscricao: true)]).IsSuccess.Should().BeTrue();
        DeclararTaxaSemCobranca(processo);

        VermelhosForaDoFormulario(processo).Should().ContainSingle()
            .Which.Codigo.Should().Be("cronograma_fase_que_coleta_inscricao_sem_janela");

        ExigirPublicacaoRecusada(processo, "ProcessoSeletivo.FaseQueColetaInscricaoSemJanela");
    }

    private static FaseCronograma FaseDeHabilitacao() => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        OrigensDoTipoDeFaseDeTeste.Da(
            Guid.CreateVersion7(), "HABILITACAO", OrigemDataFase.NaExecucao, agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(2, Inicio: null, Fim: null, UnidadeResponsavel: null),
        new PublicacoesDaFase(Produtos: [ProdutoDaFase.Criar("RESULTADO_HABILITACAO", PapelDaPublicacaoPrevista.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    [Fact(DisplayName = "Cronograma: fase sem unidade responsável — item vermelho e Publicar recusa com FaseSemUnidadeResponsavel, citando a fase")]
    public void Cronograma_FaseSemUnidadeResponsavel()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        FaseCronograma habilitacao = FaseDeHabilitacao();

        // O rascunho aceita a fase ainda sem unidade; só a publicação a exige.
        FormularioDeTeste.TrocarCronograma(processo, [FaseBaseDaImportacao(), habilitacao])
            .IsSuccess.Should().BeTrue();

        VermelhosForaDoFormulario(processo).Should().ContainSingle()
            .Which.Codigo.Should().Be("cronograma_fase_sem_unidade_responsavel");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.FaseSemUnidadeResponsavel");
        resultado.Error.Message.Should().Contain("'Habilitação'").And.NotContain("HABILITACAO");
    }

    private static FaseCronograma FaseDeMatricula() => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        OrigensDoTipoDeFaseDeTeste.Da(
            Guid.CreateVersion7(), "MATRICULA", OrigemDataFase.NaExecucao, agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(1, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    [Fact(DisplayName = "Cronograma: vagas ofertadas sem fase que produz resultado — item vermelho e Publicar recusa com VagasSemFaseQueProduzResultado")]
    public void Cronograma_VagasSemFaseQueProduzResultado()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        FormularioDeTeste.TrocarCronograma(processo, [FaseDeMatricula()]).IsSuccess.Should().BeTrue();

        // A fase que homologa as inscrições publica o resultado, então sem resultado também falta a homologação: o item da falta de resultado é um dos vermelhos.
        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo)).Where(static i => !i.Ok).Select(static i => i.Codigo)
            .Should().Contain("cronograma_vagas_sem_fase_que_produz_resultado");

        ExigirPublicacaoRecusada(processo, "ProcessoSeletivo.VagasSemFaseQueProduzResultado");
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // PendenciaDaCascata — cinco razões (cascata de remanejamento de cotas, UNI-REQ-0056)
    // ══════════════════════════════════════════════════════════════════════════════════════
}
