namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Processo mínimo conforme às seis dimensões estruturais de
/// <c>ProcessoSeletivo.ItensEstruturaisDeConformidade</c> (etapas, atendimento especializado,
/// distribuição de vagas, classificação, cronograma de fases, taxa de inscrição — issue #1112)
/// — compartilhado pelos testes dos handlers que congelam (Publicar/Retificar/FecharRetificacao),
/// que precisam de um processo que passe pelo gate estrutural antes de exercitar o gate
/// específico de cada teste.
/// </summary>
internal static class ProcessoSeletivoConformeBuilder
{
    public static readonly string HashFixo = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];

    private static FaseCronograma FaseDeResultadoFinal() => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            OrigemId: Guid.CreateVersion7(), Codigo: "RESULTADO_FINAL", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false, ColetaInscricao: true,
            ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(
            Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), FaseConcluinteCodigo: null,
            UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    public static ProcessoSeletivo Criar(string nome, out Guid faseId)
    {
        ProcessoSeletivo processo = NovoProcessoComAtendimentoEDistribuicao(nome);
        DefinirClassificacaoImportada(processo);

        FaseCronograma fase = FaseDeResultadoFinal();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        faseId = fase.Id;

        DefinirEtapaDaProvaObjetiva(processo);
        DefinirTaxaSemCobranca(processo);

        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);
        return processo;
    }

    private static ProcessoSeletivo NovoProcessoComAtendimentoEDistribuicao(string nome)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            nome, TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());

        processo.DefinirAtendimentoEspecializado(
            AtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        processo.DefinirDistribuicaoVagas([DistribuicaoAmplaConcorrencia()], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        return processo;
    }

    private static void DefinirClassificacaoImportada(ProcessoSeletivo processo)
    {
        processo.DefinirClassificacao(
            ConfiguracaoClassificacao.Criar(
                regraCalculo: ReferenciaRegra.De(RegraCalculoCodigo.ClassificacaoImportada, "v1", HashFixo),
                regraArredondamento: null,
                casasArredondamento: null,
                regraOrdemAlocacao: ReferenciaRegra.De(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "v1", HashFixo),
                regrasEliminacao: [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: [], escalaNotaFinal: null).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
    }

    private static void DefinirEtapaDaProvaObjetiva(ProcessoSeletivo processo)
    {
        processo.DefinirEtapas(
            [EtapaProcesso.Criar(
                new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(CadastrosVivos.IdentidadeDe("PROVA_OBJETIVA"), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: "RESULTADO_FINAL"),
                new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
    }

    private static void DefinirTaxaSemCobranca(ProcessoSeletivo processo)
    {
        // Issue #1112: publicar sem declarar cobrança de taxa é recusado (CA-01).
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
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
        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            modalidadeOrigemId: Guid.CreateVersion7(),
            codigo: "AC",
            descricao: null,
            naturezaLegal: NaturezaLegalModalidade.Ampla,
            composicaoVagas: ComposicaoVagasModalidade.ResidualDoVo,
            composicaoOrigemCodigo: null,
            regraRemanejamento: RegraRemanejamentoModalidade.Nenhuma,
            remanejamentoDestino: null,
            remanejamentoPar: null,
            remanejamentoFallback: null,
            regraDeAplicabilidade: regraDeAplicabilidade ?? PredicadoDnf.SemClausula,
            acaoQuandoIndeferido: null,
            baseLegal: "Res. Unifesspa 532/2021",
            quantidadeDeclarada: 40).Value!;

        return ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: ofertaCursoOrigemId ?? Guid.CreateVersion7(),
            voBase: 40,
            pr: 1m,
            regraDistribuicao: ReferenciaRegra.De(RegraDistribuicaoVagasCodigo.Institucional, "v1", HashFixo),
            regraAjuste: null,
            referenciaDemografica: null,
            modalidades: [modalidade], grupoAreaEnem: grupoAreaEnem, ofertaCurso: OfertaCursoDeTeste.Medicina).Value!;
    }
}
