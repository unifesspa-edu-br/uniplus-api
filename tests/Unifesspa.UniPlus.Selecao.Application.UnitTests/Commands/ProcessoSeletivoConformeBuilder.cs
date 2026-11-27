namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Processo mínimo conforme às seis dimensões estruturais de
/// <c>ProcessoSeletivo.ItensEstruturaisDeConformidade</c> (etapas, atendimento especializado,
/// distribuição de vagas, classificação, cronograma de fases, taxa de inscrição)
/// — compartilhado pelos testes dos handlers que congelam (Publicar/Retificar),
/// que precisam de um processo que passe pelo gate estrutural antes de exercitar o gate
/// específico de cada teste.
/// </summary>
internal static class ProcessoSeletivoConformeBuilder
{
    public static readonly string HashFixo = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];

    private static FaseCronograma FaseDeResultadoFinal(IReadOnlyList<BancaRequerida> bancasRequeridas) => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        OrigensCanonicasDeTeste.Da(
            origemId: Guid.CreateVersion7(), codigo: "RESULTADO_FINAL", origemData: OrigemDataFase.NoEdital, agrupaEtapas: true, permiteComplementacao: false, coletaInscricao: true,
            coletaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(
            Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps),
        HomologacaoDasInscricoesDeTeste.PublicacoesDaFaseQueHomologa(),
        [BancasDeTeste.DaHomologacaoDasInscricoesDaFase(), .. bancasRequeridas]).Value!;

    public static ProcessoSeletivo Criar(string nome, out Guid faseId) => Criar(nome, [], out faseId);

    /// <summary>O processo conforme cuja fase do resultado final requer as bancas dadas.</summary>
    public static ProcessoSeletivo Criar(string nome, IReadOnlyList<BancaRequerida> bancasDaFase, out Guid faseId)
    {
        ProcessoSeletivo processo = NovoProcessoComAtendimentoEDistribuicao(nome);
        DefinirClassificacaoImportada(processo);

        FaseCronograma fase = FaseDeResultadoFinal(bancasDaFase);
        processo.DefinirCronogramaFases([fase], []).IsSuccess.Should().BeTrue();
        faseId = fase.Id;

        DefinirEtapaDaProvaObjetiva(processo);
        DefinirTaxaSemCobranca(processo);

        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);
        return processo;
    }

    private static ProcessoSeletivo NovoProcessoComAtendimentoEDistribuicao(string nome)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            nome, TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());

        processo.DefinirAtendimentoEspecializado(
            AtendimentoEspecializado.Criar([], [], []).Value!)
            .IsSuccess.Should().BeTrue();

        processo.DefinirDistribuicaoVagas([DistribuicaoAmplaConcorrencia()], FatosDeModalidadeDeTeste.DoCatalogo)
            .IsSuccess.Should().BeTrue();
        return processo;
    }

    private static void DefinirClassificacaoImportada(ProcessoSeletivo processo)
    {
        processo.DefinirClassificacao(
            ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
            {
                RegraCalculo = ReferenciaRegra.De(RegraDeCalculoDaNota.ClassificacaoImportada.Codigo(), "v1", HashFixo),
                RegraOrdemAlocacao = ReferenciaRegra.De(RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria.Codigo(), "v1", HashFixo),
            }).Value!).IsSuccess.Should().BeTrue();
    }

    private static void DefinirEtapaDaProvaObjetiva(ProcessoSeletivo processo)
    {
        processo.DefinirEtapas(
            [EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
                new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(CadastrosVivos.IdentidadeDe("PROVA_OBJETIVA"), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: "RESULTADO_FINAL"),
                new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!]).IsSuccess.Should().BeTrue();
    }

    private static void DefinirTaxaSemCobranca(ProcessoSeletivo processo)
    {
        // Publicar sem declarar cobrança de taxa é recusado (CA-01).
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!).IsSuccess.Should().BeTrue();
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);
    }

    public static ProcessoSeletivo Criar(string nome) => Criar(nome, out _);

    /// <summary>Uma oferta com as 40 vagas na ampla concorrência (AC).</summary>
    /// <summary>A distribuição só com a ampla concorrência, com a regra de aplicabilidade dada ou incondicional.</summary>
    public static ConfiguracaoDistribuicaoVagas DistribuicaoAmplaConcorrencia(
        PredicadoDnf? regraDeAplicabilidade = null,
        (string? Codigo, string? Rotulo)? grupoAreaEnem = null,
        Guid? ofertaCursoOrigemId = null)
    {
        ModalidadeSelecionada modalidade = ModalidadeDeAmplaConcorrencia(regraDeAplicabilidade);

        return ConfiguracaoDistribuicaoVagas.Criar(ofertaCursoOrigemId ?? Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 40, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = ReferenciaRegra.De(RegraDeDistribuicaoDeVagas.Institucional.Codigo(), "v1", HashFixo), Modalidades = [modalidade] },
            new FatosDaOfertaNoCadastro { GrupoAreaEnem = grupoAreaEnem, OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;
    }

    private static ModalidadeSelecionada ModalidadeDeAmplaConcorrencia(PredicadoDnf? regraDeAplicabilidade)
    {
        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da("AC", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas) with { RegraRemanejamento = null, RegraDeAplicabilidade = regraDeAplicabilidade ?? PredicadoDnf.SemClausula, BaseLegal = "Res. Unifesspa 532/2021" }, 40).Value!;
        return modalidade;
    }

}
