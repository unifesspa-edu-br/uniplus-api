namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

internal static class ApoioProcessoSeletivoRestaurarConfiguracao
{
    internal static CriterioDesempate DesempatePorArea(params string[] areas) => DesempatePorArea(1, areas);

    internal static CriterioDesempate DesempatePorArea(int ordem, params string[] areas) =>
        CriterioDesempate.Criar(
            ordem, Regra(CriterioDesempateCodigo.MaiorNotaAreaEnem, 'e'), new ArgsDesempateMaiorNotaAreaEnem(areas)).Value!;

    internal static EtapaProcesso EtapaNotaEnemCongelada(Guid id, int ordem) =>
        EtapaProcesso.Reidratar(
            id,
            new IdentificacaoDaEtapa(Nome: "Nota do ENEM", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "NOTA_ENEM", "Nota do ENEM", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: true).Value!, Ordem: ordem, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null),
            itens: null);

    internal static ReferenciaRegra Regra(string codigo, char semente) =>
        ReferenciaRegra.De(codigo, "v1", new string(semente, 64));

    internal static ProcessoSeletivo ProcessoConforme(TipoProcessoSnapshot tipo)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Restauração", tipo, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());

        processo.DefinirCronogramaFases([FaseConforme()], [], PrecondicaoIfMatch.Ausente);
        processo.DefinirEtapas([
            EtapaCongeladaDeProvaObjetiva(EtapaOriginal, "Prova Original", ordem: 1, faseCodigo: "RESULTADO_FINAL"),
        ], PrecondicaoIfMatch.Ausente);
        processo.DefinirAtendimentoEspecializado(AtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente);
        processo.DefinirDistribuicaoVagas([Distribuicao()], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente);
        processo.DefinirClassificacao(Classificacao([]), PrecondicaoIfMatch.Ausente);

        // Issue #1112: publicar sem declarar cobrança de taxa é recusado (CA-01).
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!,
            PrecondicaoIfMatch.Ausente);
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);

        return processo;
    }

    internal static ProcessoSeletivo ProcessoPublicado(TipoProcessoSnapshot tipo)
    {
        ProcessoSeletivo processo = ProcessoConforme(tipo);

        processo.Publicar(
            new CenarioDeCongelamento { Dados = Dados(), Documento = new DocumentoCongelado([1, 2, 3], "1.1", "canonical-json/sha256@v1", new string('a', 64)), AtorUsuarioContaId = "testes" }).IsSuccess.Should().BeTrue();

        processo.ClearDomainEvents();
        return processo;
    }

    /// <summary>
    /// A versão que autentica a reposição. Os bytes não importam neste nível — a prova de
    /// que o grafo veio <b>daquela</b> versão é do <c>RestauradorDeConfiguracao</c>
    /// (Application), que recanonicaliza; o Domain não canonicaliza (ADR-0042).
    /// </summary>
    internal static VersaoConfiguracao VersaoDo(ProcessoSeletivo processo) => VersaoConfiguracao.Abrir(
        processo.Id, new DocumentoCongelado([1, 2, 3], "1.1", "canonical-json/sha256@v1", new string('a', 64)),
        atoCriadorId: Guid.CreateVersion7(), atorUsuarioContaId: "testes", instante: DateTimeOffset.UnixEpoch);

    internal static GrafoConfiguracao GrafoComBonus(ConfiguracaoBonusRegional bonus) => new(
        etapas: [EtapaCongeladaDeProvaObjetiva(EtapaCongelada, "Prova", ordem: 1)],
        atendimentoEspecializado: AtendimentoEspecializado.Criar([], [], []).Value!,
        distribuicaoVagas: [Distribuicao()],
        bonusRegional: bonus,
        criteriosDesempate: [],
        classificacao: Classificacao([]),
        cronogramaFases: [FaseConforme()],
        documentosExigidos: [],
        nosExigencia: [],
        referenciaTemporalFatos: null,
        numeroDeOpcoesDeCurso: 1);

    internal static GrafoConfiguracao Grafo(
        IReadOnlyList<EtapaProcesso>? etapas = null,
        IReadOnlyList<CriterioDesempate>? criterios = null,
        IReadOnlyList<RegraEliminacao>? eliminacoes = null,
        IReadOnlyList<FaseCronograma>? cronogramaFases = null,
        ConfiguracaoDivulgacao? configuracaoDivulgacao = null,
        ConfiguracaoClassificacao? classificacao = null,
        IReadOnlyList<FatoColetado>? fatosColetados = null,
        int? numeroDeOpcoesDeCurso = 1) => new(
            etapas: etapas ?? [EtapaCongeladaDeProvaObjetiva(EtapaCongelada, "Prova", ordem: 1)],
            atendimentoEspecializado: AtendimentoEspecializado.Criar([], [], []).Value!,
            distribuicaoVagas: [Distribuicao()],
            bonusRegional: null,
            criteriosDesempate: criterios ?? [],
            classificacao: classificacao ?? Classificacao(eliminacoes ?? []),
            cronogramaFases: cronogramaFases ?? [FaseConforme()],
            documentosExigidos: [],
            nosExigencia: [],
            referenciaTemporalFatos: null,
            configuracaoDivulgacao: configuracaoDivulgacao,
            fatosColetados: fatosColetados,
            numeroDeOpcoesDeCurso: numeroDeOpcoesDeCurso);

    /// <summary>
    /// Mesmo grafo de <see cref="Grafo"/>, com <c>documentosExigidos</c>/<c>nosExigencia</c>
    /// explícitos — usado pelos testes de árvore de satisfação (Story #923), que precisam de
    /// uma fase congelada específica para <paramref name="documentosExigidos"/> referenciar.
    /// </summary>
    internal static GrafoConfiguracao GrafoComArvore(
        FaseCronograma fase, IReadOnlyList<DocumentoExigido> documentosExigidos, IReadOnlyList<NoExigencia> nosExigencia) => new(
            etapas: [EtapaCongeladaDeProvaObjetiva(EtapaCongelada, "Prova", ordem: 1)],
            atendimentoEspecializado: AtendimentoEspecializado.Criar([], [], []).Value!,
            distribuicaoVagas: [Distribuicao()],
            bonusRegional: null,
            criteriosDesempate: [],
            classificacao: Classificacao([]),
            cronogramaFases: [fase],
            documentosExigidos: documentosExigidos,
            nosExigencia: nosExigencia,
            referenciaTemporalFatos: null,
            numeroDeOpcoesDeCurso: 1);

    internal static DocumentoExigido DocumentoQualquer(Guid faseId) => DocumentoExigido.Criar(
        faseId, Guid.CreateVersion7(), "IDENTIDADE", "Documento de identidade", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal,
        Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null, [], [], null,
        FormatosPermitidos.Criar(true, null).Value!, null).Value!;

    /// <summary>Uma fase mínima e conforme: agrupa etapas (há 1 etapa por padrão) e produz resultado (há vagas por padrão).</summary>
    internal static FaseCronograma FaseConforme() => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            OrigemId: new Guid("eeee0000-0000-4000-8000-000000000001"), Codigo: "RESULTADO_FINAL", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false,
            ColetaInscricao: false, ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(
            Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), FaseConcluinteCodigo: null,
            UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    internal static ConfiguracaoDistribuicaoVagas Distribuicao() =>
        ConfiguracaoDistribuicaoVagas.Criar(new Guid("bbbb0000-0000-4000-8000-000000000001"),
            new DistribuicaoDeVagasDeclarada
            {
                VagasOfertadasBase = 40,
                PercentualDaReservaLegal = 1m,
                RegraDeDistribuicao = Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'),
                Modalidades = [
                ModalidadeSelecionada.Criar(
                    new Guid("cccc0000-0000-4000-8000-000000000001"), "AC", null,
                    NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas, null,
                    RegraRemanejamentoModalidade.Nenhuma, null, null, null,
                    RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), null, "Res. Unifesspa 532/2021", quantidadeDeclarada: 40).Value!,
            ]
            },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;

    internal static ConfiguracaoClassificacao Classificacao(IReadOnlyList<RegraEliminacao> eliminacoes) =>
        ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = Regra(RegraCalculoCodigo.FormulaMediaPonderada, 'b'),
            RegraArredondamento = Regra(RegraArredondamentoCodigo.PrecisaoTruncar, 'c'),
            CasasArredondamento = 2,
            RegraOrdemAlocacao = Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'd'),
            RegrasEliminacao = eliminacoes,
            EscalaNotaFinal = 100m,
        }).Value!;

    internal static ConfiguracaoClassificacao ClassificacaoEnemMediaPonderada(IReadOnlyList<RegraEliminacao>? eliminacoes = null) =>
        ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = Regra(RegraCalculoCodigo.FormulaMediaPonderada, 'b'),
            RegraArredondamento = Regra(RegraArredondamentoCodigo.PrecisaoTruncar, 'c'),
            CasasArredondamento = 2,
            RegraOrdemAlocacao = Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'd'),
            RegrasEliminacao = eliminacoes ?? [],
            BaseadoEmEnem = true,
            ResolucaoPesoAreaEnem = QuadroPesoAreaEnemDeTeste.Resolucao,
            QuadroPesoAreaEnem = QuadroPesoAreaEnemDeTeste.Completo(),
            EscalaNotaFinal = 100m,
        }).Value!;

    internal static DadosEdital Dados() => DadosEdital.Criar(
        "001/2026",
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
        new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)),
        new Guid("dddd0000-0000-4000-8000-000000000001")).Value!;

    /// <summary>
    /// Snapshot das <b>seis dimensões</b> mais o status — é sobre ele que o critério de aceite de
    /// restauração tudo-ou-nada (Story #859, CA-07) asserta. Comparar só o <c>Result</c> deixaria
    /// passar exatamente a implementação que esse critério existe para proibir: a que aplica
    /// dimensão a dimensão e só depois falha, deixando o agregado meio-reposto.
    /// </summary>
    internal sealed record Estado(
        StatusProcesso Status,
        IReadOnlyList<(Guid Id, string Nome, decimal? Peso, int? Ordem)> Etapas,
        int Condicoes,
        IReadOnlyList<(Guid Oferta, int VagasOfertadasBase, decimal? PercentualDaReservaLegal, int Modalidades)> Distribuicao,
        bool TemBonus,
        IReadOnlyList<int> OrdensDesempate,
        string RegraCalculo,
        int Eliminacoes)
    {
        internal static Estado De(ProcessoSeletivo processo) => new(
            processo.Status,
            [.. processo.Etapas.Select(e => (e.Id, e.Nome, e.Peso, e.Ordem)).OrderBy(e => e.Id)],
            processo.AtendimentoEspecializado!.Condicoes.Count,
            [.. processo.DistribuicaoVagas
                .Select(d => (d.OfertaCursoOrigemId, d.VagasOfertadasBase, d.PercentualDaReservaLegal, d.Modalidades.Count))
                .OrderBy(d => d.OfertaCursoOrigemId)],
            processo.BonusRegional is not null,
            [.. processo.CriteriosDesempate.Select(c => c.Ordem).Order()],
            processo.Classificacao!.RegraCalculo.Codigo,
            processo.Classificacao.RegrasEliminacao.Count);
    }
    internal static readonly Guid EtapaOriginal = new("aaaa0000-0000-4000-8000-000000000001");
    internal static readonly Guid EtapaCongelada = new("aaaa0000-0000-4000-8000-000000000002");

    internal static EtapaProcesso EtapaCongeladaDeProvaObjetiva(
        Guid id, string nome, int ordem, string? faseCodigo = null, decimal peso = 1m, decimal? notaMinima = null,
        CaraterEtapa carater = CaraterEtapa.Classificatoria) =>
        EtapaProcesso.Reidratar(
            id,
            new IdentificacaoDaEtapa(Nome: nome, Carater: carater, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: ordem, FaseCodigo: faseCodigo),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: peso, NotaMinima: notaMinima, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null),
            itens: null);
}
