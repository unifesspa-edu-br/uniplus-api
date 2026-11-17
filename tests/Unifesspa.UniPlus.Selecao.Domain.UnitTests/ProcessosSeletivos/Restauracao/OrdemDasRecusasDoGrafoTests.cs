namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Restauracao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A primeira causa da restauração, na ordem em que o grafo é conferido. Cada recusa é provada sozinha e contra a seguinte
/// que pode coexistir com ela no mesmo grafo: quem corrige o grafo de cima para baixo vê sempre a primeira causa. A etapa
/// sem fase e a fase que agrupa etapas sem etapa são completude e não entram: ver <c>RecusasAcumuladasNaRestauracaoTests</c>.
/// </summary>
public sealed class OrdemDasRecusasDoGrafoTests
{
    private static readonly Dictionary<int, string> CodigoDaRecusa = new()
    {
        [1] = "ProcessoSeletivo.IdEtapaAusente",
        [2] = "ProcessoSeletivo.IdEtapaDuplicado",
        [3] = "ProcessoSeletivo.OrdemEtapaDuplicada",
        [4] = "ProcessoSeletivo.NenhumaEtapaComponeNota",
        [5] = "ProcessoSeletivo.DistribuicaoVagasVazia",
        [6] = "ProcessoSeletivo.OfertaCursoDuplicada",
        [7] = "ProcessoSeletivo.OpcoesDeCursoInvalido",
        [8] = "ProcessoSeletivo.OrdemDesempateDuplicada",
        [9] = "ProcessoSeletivo.EtapaRefDesempateInexistente",
        [10] = "ProcessoSeletivo.EtapaRefEliminacaoInexistente",
        [11] = "ProcessoSeletivo.CronogramaFasesVazio",
        [12] = "ProcessoSeletivo.OrdemFaseDuplicada",
        [13] = "ProcessoSeletivo.FaseCanonicaDuplicada",
        [14] = "ProcessoSeletivo.EtapaSemFaseNoCronograma",
    };

    public static TheoryData<int[]> Cenarios() => new()
    {
        { [1] }, { [2] }, { [3] }, { [4] }, { [5] }, { [6] }, { [7] }, { [8] }, { [9] }, { [10] },
        { [11] }, { [12] }, { [13] }, { [14] },
        { [1, 2] }, { [2, 3] }, { [3, 4] }, { [4, 5] }, { [4, 6] }, { [5, 7] }, { [6, 7] }, { [7, 8] },
        { [8, 9] }, { [9, 10] }, { [10, 11] }, { [10, 12] }, { [12, 13] }, { [13, 14] },
    };

    [Theory(DisplayName = "A restauração recusa com a primeira causa do grafo, na ordem da conferência")]
    [MemberData(nameof(Cenarios))]
    public void Restaurar_GrafoComProblemas_RecusaComAPrimeiraCausa(int[] problemas)
    {
        ProcessoSeletivo processo = ProcessoEmEdicaoDeTeste.PublicadoSemSessao(out VersaoConfiguracao versao);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, GrafoComProblemas(problemas));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CodigoDaRecusa[problemas.Min()]);
    }

    internal static GrafoConfiguracao GrafoComProblemas(int[] problemas)
    {
        GrafoEmMontagem montagem = new();

        foreach (int problema in problemas.Distinct().Order())
        {
            Problemas[problema](montagem);
        }

        return montagem.Grafo();
    }

    private static readonly Dictionary<int, Action<GrafoEmMontagem>> Problemas = new()
    {
        [1] = static g => g.Etapas.Add(SemId(Etapa(Guid.CreateVersion7(), 7, CaraterEtapa.Classificatoria, peso: 1m))),
        [2] = static g => { Guid repetido = Guid.CreateVersion7(); g.Etapas.AddRange([Etapa(repetido, 2), Etapa(repetido, 3)]); },
        [3] = static g => g.Etapas.AddRange([Etapa(Guid.CreateVersion7(), 4), Etapa(Guid.CreateVersion7(), 4)]),
        [4] = static g => g.Etapas.Add(Etapa(Guid.CreateVersion7(), 5)),
        [5] = static g => g.Distribuicao.Clear(),
        [6] = static g => g.Distribuicao.Add(Oferta(g.Distribuicao[0].OfertaCursoOrigemId)),
        [7] = static g => g.Opcoes = 0,
        [8] = static g => g.Desempate.AddRange([Desempate(1), Desempate(1)]),
        [9] = static g => g.Desempate.Add(DesempatePorEtapa(2, Guid.CreateVersion7())),
        [10] = static g => g.Eliminacoes.Add(EliminacaoPorNotaMinima(Guid.CreateVersion7())),
        [11] = static g => g.Fases.Clear(),
        [12] = static g => g.Fases.AddRange([Fase(2, agrupaEtapas: false), Fase(2, agrupaEtapas: false)]),
        [13] = static g => { Guid origem = Guid.CreateVersion7(); g.Fases.AddRange([Fase(3, false, origem), Fase(4, false, origem)]); },
        [14] = static g => g.Etapas.Add(Etapa(Guid.CreateVersion7(), 8, CaraterEtapa.Classificatoria, peso: 1m, faseCodigo: "FASE_INEXISTENTE")),
        [15] = static g => g.Fases.Add(Fase(5, agrupaEtapas: true)),
        [16] = static g => g.Etapas.Add(Etapa(Guid.CreateVersion7(), 6, CaraterEtapa.Classificatoria, peso: 1m)),
    };

    private sealed class GrafoEmMontagem
    {
        internal List<EtapaProcesso> Etapas { get; } = [];

        internal List<ConfiguracaoDistribuicaoVagas> Distribuicao { get; } = [Oferta()];

        internal List<CriterioDesempate> Desempate { get; } = [];

        internal List<RegraEliminacao> Eliminacoes { get; } = [];

        internal List<FaseCronograma> Fases { get; } = [Fase(1, agrupaEtapas: false)];

        internal int? Opcoes { get; set; } = 1;

        internal GrafoConfiguracao Grafo() => new(
            etapas: Etapas,
            atendimentoEspecializado: AtendimentoEspecializado.Criar([], [], []).Value!,
            distribuicaoVagas: Distribuicao,
            bonusRegional: null,
            criteriosDesempate: Desempate,
            classificacao: Classificacao(Eliminacoes),
            cronogramaFases: Fases,
            documentosExigidos: [],
            nosExigencia: [],
            referenciaTemporalFatos: null,
            numeroDeOpcoesDeCurso: Opcoes);
    }

    // Reidratar recusa o Id vazio: o estado que a restauração confere é o de uma etapa que chegou ao grafo sem identidade.
    private static EtapaProcesso SemId(EtapaProcesso etapa)
    {
        typeof(EntityBase).GetProperty(nameof(EntityBase.Id))!.SetValue(etapa, Guid.Empty);

        return etapa;
    }

    private static ReferenciaRegra Regra(string codigo, char semente) =>
        ReferenciaRegra.De(codigo, "v1", new string(semente, 64));

    private static EtapaProcesso Etapa(Guid id, int ordem, CaraterEtapa carater = CaraterEtapa.Eliminatoria, decimal? peso = null, string? faseCodigo = null) =>
        EtapaProcesso.Reidratar(
            id,
            new IdentificacaoDaEtapa(Nome: $"Etapa {ordem}", Carater: carater, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: ordem, FaseCodigo: faseCodigo),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: peso, NotaMinima: 5m, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null),
            itens: null);

    private static ConfiguracaoDistribuicaoVagas Oferta(Guid? ofertaCursoOrigemId = null) =>
        ConfiguracaoDistribuicaoVagas.Criar(ofertaCursoOrigemId ?? Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada
            {
                VagasOfertadasBase = 40,
                PercentualDaReservaLegal = 1m,
                RegraDeDistribuicao = Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'),
                Modalidades = [
                ModalidadeSelecionada.Criar(
                    Guid.CreateVersion7(), "AC", null,
                    NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas, null,
                    RegraRemanejamentoModalidade.Nenhuma, null, null, null,
                    RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), null, "Res. Unifesspa 532/2021", quantidadeDeclarada: 40).Value!,
            ]
            },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;

    private static CriterioDesempate Desempate(int ordem) =>
        CriterioDesempate.Criar(ordem, Regra(CriterioDesempateCodigo.MaiorIdade, 'f'), new ArgsDesempateMaiorIdade()).Value!;

    private static CriterioDesempate DesempatePorEtapa(int ordem, Guid etapaRef) =>
        CriterioDesempate.Criar(ordem, Regra(CriterioDesempateCodigo.MaiorNotaEtapa, 'd'), new ArgsDesempateMaiorNotaEtapa(etapaRef)).Value!;

    private static RegraEliminacao EliminacaoPorNotaMinima(Guid etapaRef) =>
        RegraEliminacao.Criar(Regra(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, 'e'), new ArgsElimNotaMinimaEtapa(etapaRef, 4m)).Value!;

    private static ConfiguracaoClassificacao Classificacao(IReadOnlyList<RegraEliminacao> eliminacoes) =>
        ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = Regra(RegraCalculoCodigo.FormulaMediaPonderada, 'b'),
            RegraArredondamento = Regra(RegraArredondamentoCodigo.PrecisaoTruncar, 'c'),
            CasasArredondamento = 2,
            RegraOrdemAlocacao = Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'd'),
            RegrasEliminacao = eliminacoes,
            EscalaNotaFinal = 100m,
        }).Value!;

    private static FaseCronograma Fase(int ordem, bool agrupaEtapas, Guid? faseCanonicaOrigemId = null) => FaseCronograma.Reidratar(
        Guid.CreateVersion7(),
        new OrigemCanonicaDaFase(
            faseCanonicaOrigemId ?? Guid.CreateVersion7(), $"FASE_{ordem}", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: agrupaEtapas, PermiteComplementacao: false, ColetaInscricao: false,
            ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(
            ordem, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), FaseConcluinteCodigo: null,
            UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []);
}
