namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Restauracao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A restauração para na primeira recusa, na ordem em que o grafo é conferido. Cada recusa é
/// provada sozinha e contra a seguinte que pode coexistir com ela no mesmo grafo: quem corrige
/// o grafo de cima para baixo vê sempre o primeiro problema.
/// </summary>
public sealed class OrdemDasRecusasDoGrafoTests
{
    private static readonly Dictionary<int, string> CodigoDaRecusa = new()
    {
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
        [14] = "ProcessoSeletivo.AvaliacaoSemEtapa",
        [15] = "ProcessoSeletivo.EtapaSemFaseDeAvaliacao",
    };

    public static TheoryData<int[]> Cenarios() => new()
    {
        { [2] }, { [3] }, { [4] }, { [5] }, { [6] }, { [7] }, { [8] }, { [9] }, { [10] },
        { [11] }, { [12] }, { [13] }, { [14] }, { [15] },
        { [2, 3] }, { [3, 4] }, { [4, 5] }, { [4, 6] }, { [5, 7] }, { [6, 7] }, { [7, 8] },
        { [8, 9] }, { [9, 10] }, { [10, 11] }, { [10, 12] }, { [12, 13] }, { [13, 14] }, { [13, 15] },
    };

    [Theory(DisplayName = "A restauração recusa o primeiro problema do grafo, na ordem da conferência")]
    [MemberData(nameof(Cenarios))]
    public void Restaurar_GrafoComProblemas_RecusaOPrimeiro(int[] problemas)
    {
        ProcessoSeletivo processo = ProcessoEmEdicaoDeTeste.PublicadoSemSessao(out VersaoConfiguracao versao);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, GrafoComProblemas(problemas));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CodigoDaRecusa[problemas.Min()]);
    }

    internal static GrafoConfiguracao GrafoComProblemas(int[] problemas)
    {
        List<EtapaProcesso> etapas = [];
        List<ConfiguracaoDistribuicaoVagas> distribuicao = [Oferta()];
        List<CriterioDesempate> desempate = [];
        List<RegraEliminacao> eliminacoes = [];
        List<FaseCronograma> fases = [Fase(1, agrupaEtapas: false)];
        int? opcoes = 1;

        if (problemas.Contains(2)) { Guid repetido = Guid.CreateVersion7(); etapas.AddRange([Etapa(repetido, 2), Etapa(repetido, 3)]); }
        if (problemas.Contains(3)) { etapas.AddRange([Etapa(Guid.CreateVersion7(), 4), Etapa(Guid.CreateVersion7(), 4)]); }
        if (problemas.Contains(4)) { etapas.Add(Etapa(Guid.CreateVersion7(), 5)); }
        if (problemas.Contains(5)) { distribuicao.Clear(); }
        if (problemas.Contains(6)) { distribuicao.Add(Oferta(distribuicao[0].OfertaCursoOrigemId)); }
        if (problemas.Contains(7)) { opcoes = 0; }
        if (problemas.Contains(8)) { desempate.AddRange([Desempate(1), Desempate(1)]); }
        if (problemas.Contains(9)) { desempate.Add(DesempatePorEtapa(2, Guid.CreateVersion7())); }
        if (problemas.Contains(10)) { eliminacoes.Add(EliminacaoPorNotaMinima(Guid.CreateVersion7())); }
        if (problemas.Contains(11)) { fases.Clear(); }
        if (problemas.Contains(12)) { fases.AddRange([Fase(2, agrupaEtapas: false), Fase(2, agrupaEtapas: false)]); }
        if (problemas.Contains(13)) { Guid origem = Guid.CreateVersion7(); fases.AddRange([Fase(3, false, origem), Fase(4, false, origem)]); }
        if (problemas.Contains(14)) { fases.Add(Fase(5, agrupaEtapas: true)); }
        if (problemas.Contains(15)) { etapas.Add(Etapa(Guid.CreateVersion7(), 6, CaraterEtapa.Classificatoria, peso: 1m)); }

        return new GrafoConfiguracao(
            etapas: etapas,
            ofertaAtendimento: OfertaAtendimentoEspecializado.Criar([], [], []).Value!,
            distribuicaoVagas: distribuicao,
            bonusRegional: null,
            criteriosDesempate: desempate,
            classificacao: Classificacao(eliminacoes),
            cronogramaFases: fases,
            documentosExigidos: [],
            nosExigencia: [],
            referenciaTemporalFatos: null,
            numeroDeOpcoesDeCurso: opcoes);
    }

    private static ReferenciaRegra Regra(string codigo, char semente) =>
        ReferenciaRegra.Criar(codigo, "v1", new string(semente, 64)).Value!;

    private static EtapaProcesso Etapa(Guid id, int ordem, CaraterEtapa carater = CaraterEtapa.Eliminatoria, decimal? peso = null) =>
        EtapaProcesso.Reidratar(id, $"Etapa {ordem}", carater, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, peso, 5m, ordem, notaMaxima: 10m, formaDeAvaliacao: FormaDeAvaliacao.Objetiva);

    private static ConfiguracaoDistribuicaoVagas Oferta(Guid? ofertaCursoOrigemId = null) =>
        ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: ofertaCursoOrigemId ?? Guid.CreateVersion7(),
            voBase: 40,
            pr: 1m,
            regraDistribuicao: Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'),
            regraAjuste: null,
            referenciaDemografica: null,
            modalidades: [
                ModalidadeSelecionada.Criar(
                    Guid.CreateVersion7(), "AC", null,
                    NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo, null,
                    RegraRemanejamentoModalidade.Nenhuma, null, null, null,
                    RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), null, "Res. Unifesspa 532/2021", quantidadeDeclarada: 40).Value!,
            ], ofertaCurso: OfertaCursoDeTeste.Medicina).Value!;

    private static CriterioDesempate Desempate(int ordem) =>
        CriterioDesempate.Criar(ordem, Regra(CriterioDesempateCodigo.MaiorIdade, 'f'), new ArgsDesempateMaiorIdade()).Value!;

    private static CriterioDesempate DesempatePorEtapa(int ordem, Guid etapaRef) =>
        CriterioDesempate.Criar(ordem, Regra(CriterioDesempateCodigo.MaiorNotaEtapa, 'd'), new ArgsDesempateMaiorNotaEtapa(etapaRef)).Value!;

    private static RegraEliminacao EliminacaoPorNotaMinima(Guid etapaRef) =>
        RegraEliminacao.Criar(Regra(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, 'e'), new ArgsElimNotaMinimaEtapa(etapaRef, 4m)).Value!;

    private static ConfiguracaoClassificacao Classificacao(IReadOnlyList<RegraEliminacao> eliminacoes) =>
        ConfiguracaoClassificacao.Criar(
            regraCalculo: Regra(RegraCalculoCodigo.FormulaMediaPonderada, 'b'),
            regraArredondamento: Regra(RegraArredondamentoCodigo.PrecisaoTruncar, 'c'),
            casasArredondamento: 2,
            regraOrdemAlocacao: Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'd'),
            regrasEliminacao: eliminacoes,
            baseadoEmEnem: false,
            resolucaoPesoAreaEnem: null,
            quadroPesoAreaEnem: [], escalaNotaFinal: 100m).Value!;

    private static FaseCronograma Fase(int ordem, bool agrupaEtapas, Guid? faseCanonicaOrigemId = null) => FaseCronograma.Reidratar(
        Guid.CreateVersion7(), ordem, faseCanonicaOrigemId ?? Guid.CreateVersion7(), $"FASE_{ordem}",
        origemData: OrigemDataFase.NoEdital, agrupaEtapas: agrupaEtapas,
        permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false,
        inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
        produtos: [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)],
        faseConcluinteCodigo: null,
        emiteParecerIndividual: false,
        bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps);
}
