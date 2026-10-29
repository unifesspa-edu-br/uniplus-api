namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// As conferências de publicação que antes olhavam só as fases, estendidas às etapas, e as
/// recusas do tipo de ato declarado contra a classificação dele no catálogo (UNI-REQ-0157).
/// </summary>
public sealed class ConferenciasDoCronogramaNasEtapasTests
{
    private static readonly string HashFixo = new('a', 64);

    private static ProcessoSeletivo ProcessoConforme(FaseCronograma? fase = null) =>
        ProcessoConformeFactory.Criar(fase: fase);

    private static ReferenciaRegra Algoritmo() => ReferenciaRegra.Criar(
        AlgoritmoContagemPrazoCodigo.ExcluiDiaInicial, "v1", new string('b', 64)).Value!;

    private static ContextoDeContagemDePrazos ComCalendario() => new(
        CalendarioDiasUteisCongelado.Criar(
            Guid.CreateVersion7(),
            "2026",
            [DiaNaoUtilCongelado.Criar(new DateOnly(2026, 1, 1), "NACIONAL", null, null, null).Value!]).Value,
        FusoInstitucional: TimeZoneInfo.FindSystemTimeZoneById(FusoInstitucional.ZoneId));

    private static Result<VersaoConfiguracao> Publicar(
        ProcessoSeletivo processo,
        ContextoDeContagemDePrazos contexto,
        TiposDeAtoUnicosPorObjeto? tipos = null) => processo.Publicar(
        ProcessoConformeFactory.Dados(), "{}"u8.ToArray(), "1.1", "canonical-json/sha256@v1", HashFixo, "teste",
        TimeProvider.System, contexto, FatosDeModalidadeDeTeste.DoCatalogo, tipos ?? TiposDeAtoUnicosDeTeste.Nenhum);

    private static IEnumerable<string> Vermelhos(ProcessoSeletivo processo, ContextoDeContagemDePrazos contexto, TiposDeAtoUnicosPorObjeto? tipos = null) =>
        processo.AvaliarConformidade(contexto, FatosDeModalidadeDeTeste.DoCatalogo, tipos ?? TiposDeAtoUnicosDeTeste.Nenhum)
            .Where(static i => !i.Ok).Select(static i => i.Codigo);

    private static RecursoDaEtapa Janela(Guid produtoAncoraId) => RecursoDaEtapa.Criar(
        AncoraDoRecurso.AtoPublicado,
        ReferenciaRegra.Criar(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('c', 64)).Value!,
        new ArgsRegraPrazoRecurso(2m, UnidadePrazo.DiasUteis, null, null, false, null, null),
        produtoAncoraId).Value!;

    /// <summary>
    /// Dá à única etapa do processo o par preliminar e definitivo do mesmo tipo e uma janela de
    /// recurso em dias úteis ancorada no preliminar. A etapa é mutada direto, como o EF a
    /// hidrataria: o que se prova aqui são os gates do agregado, não a gravação.
    /// </summary>
    private static EtapaProcesso ComRecursoNaEtapa(ProcessoSeletivo processo)
    {
        EtapaProcesso etapa = processo.Etapas.Single();
        etapa.DefinirProdutos(
        [
            ProdutoDaEtapa.Criar("RESULTADO_PROVA", PapelProdutoFase.Preliminar),
            ProdutoDaEtapa.Criar("RESULTADO_PROVA", PapelProdutoFase.Definitivo),
        ]).IsSuccess.Should().BeTrue();
        Guid preliminar = etapa.Produtos.Single(p => p.Papel == PapelProdutoFase.Preliminar).Id;
        etapa.DefinirRecursos([Janela(preliminar)]).IsSuccess.Should().BeTrue();
        return etapa;
    }

    [Fact(DisplayName = "Recurso só na etapa, em dias úteis: sem convenção de contagem a publicação é recusada e o item fica vermelho")]
    public void RecursoSoNaEtapa_SemAlgoritmo_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        ComRecursoNaEtapa(processo);

        Vermelhos(processo, ComCalendario()).Should().Contain("algoritmo_contagem_prazo_nao_declarado");

        Result<VersaoConfiguracao> publicar = Publicar(processo, ComCalendario());

        publicar.IsFailure.Should().BeTrue();
        publicar.Error!.Code.Should().Be("ProcessoSeletivo.AlgoritmoContagemPrazoNaoDeclarado");
    }

    [Fact(DisplayName = "Recurso só na etapa, em dias úteis: sem calendário vigente a publicação é recusada e o item fica vermelho")]
    public void RecursoSoNaEtapa_SemCalendario_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        ComRecursoNaEtapa(processo);
        processo.DefinirAlgoritmoContagemPrazo(Algoritmo(), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Vermelhos(processo, ContextoDeContagemDePrazos.SemCalendario).Should().Contain("calendario_vigente_ausente");

        Result<VersaoConfiguracao> publicar = Publicar(processo, ContextoDeContagemDePrazos.SemCalendario);

        publicar.IsFailure.Should().BeTrue();
        publicar.Error!.Code.Should().Be("ProcessoSeletivo.CalendarioVigenteAusente");
    }

    [Fact(DisplayName = "Etapa com a janela ancorada fora dos preliminares dela, montada sem a fábrica: item vermelho e a publicação recusa")]
    public void AncoraDaEtapaForaDosPreliminares_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        EtapaProcesso etapa = ComRecursoNaEtapa(processo);
        processo.DefinirAlgoritmoContagemPrazo(Algoritmo(), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        // O estado só existe em processo carregado do banco: DefinirRecursos o recusa na gravação.
        Guid definitivo = etapa.Produtos.Single(p => p.Papel == PapelProdutoFase.Definitivo).Id;
        typeof(EtapaProcesso).GetField("_recursos", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(etapa).Should().BeAssignableTo<List<RecursoDaEtapa>>().Which
            .Add(Janela(definitivo));

        Vermelhos(processo, ComCalendario()).Should().Equal("cronograma_ancora_do_recurso_da_etapa");

        Result<VersaoConfiguracao> publicar = Publicar(processo, ComCalendario());

        publicar.IsFailure.Should().BeTrue();
        publicar.Error!.Code.Should().Be("EtapaProcesso.AncoraNaoEhProdutoPreliminarDaEtapa");
    }

    [Fact(DisplayName = "Etapa que publica o preliminar sem o definitivo do mesmo tipo: a gravação das etapas recusa")]
    public void EtapaComPreliminarSemDefinitivo_GravacaoRecusa()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        EtapaProcesso etapa = processo.Etapas.Single();
        etapa.DefinirProdutos(
        [
            ProdutoDaEtapa.Criar("GABARITO", PapelProdutoFase.Preliminar),
            ProdutoDaEtapa.Criar("RESULTADO_PROVA", PapelProdutoFase.Definitivo),
        ]).IsSuccess.Should().BeTrue();

        Result gravar = processo.DefinirEtapas([etapa], PrecondicaoIfMatch.Curinga);

        gravar.IsFailure.Should().BeTrue();
        gravar.Errors.Should().ContainSingle().Which.Error.Code.Should().Be("ProcessoSeletivo.EtapaComPreliminarSemDefinitivo");
    }

    [Fact(DisplayName = "Etapa que publica o preliminar sem o definitivo do mesmo tipo, carregada do banco: item vermelho e a publicação recusa")]
    public void EtapaComPreliminarSemDefinitivo_PublicacaoRecusa()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        processo.Etapas.Single().DefinirProdutos(
        [
            ProdutoDaEtapa.Criar("GABARITO", PapelProdutoFase.Preliminar),
            ProdutoDaEtapa.Criar("RESULTADO_PROVA", PapelProdutoFase.Definitivo),
        ]).IsSuccess.Should().BeTrue();

        Vermelhos(processo, ContextoDeContagemDePrazos.SemCalendario).Should().Equal("cronograma_conclusao_do_ciclo_recursal");

        Result<VersaoConfiguracao> publicar = Publicar(processo, ContextoDeContagemDePrazos.SemCalendario);

        publicar.IsFailure.Should().BeTrue();
        publicar.Error!.Code.Should().Be("ProcessoSeletivo.EtapaComPreliminarSemDefinitivo");
    }

    [Fact(DisplayName = "Fase que já publica o definitivo do tipo e aponta uma concluinte que o publica também: o cronograma recusa")]
    public void FaseComConcluinteQueRepeteODefinitivo_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Cronograma", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma abre = FaseCronograma.Criar(
            1, Guid.CreateVersion7(), "RESULTADO_PRELIMINAR", OrigemDataFase.NaExecucao,
            agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false, inicio: null, fim: null,
            produtos:
            [
                ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar),
                ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo),
            ],
            faseConcluinteCodigo: "RESULTADO_FINAL", emiteParecerIndividual: false,
            bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;
        FaseCronograma conclui = FaseCronograma.Criar(
            2, Guid.CreateVersion7(), "RESULTADO_FINAL", OrigemDataFase.NaExecucao,
            agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false, inicio: null, fim: null,
            produtos: [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)],
            faseConcluinteCodigo: null, emiteParecerIndividual: false,
            bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;

        Result gravar = processo.DefinirCronogramaFases([abre, conclui], [], PrecondicaoIfMatch.Curinga);

        gravar.IsFailure.Should().BeTrue();
        gravar.Errors.Should().ContainSingle().Which.Error.Code.Should().Be("ProcessoSeletivo.ConclusaoDuplicada");
    }

    private static TiposDeAtoUnicosPorObjeto Catalogo(string codigo, bool ehResultado, bool congela) =>
        TiposDeAtoUnicosPorObjeto.ParaChecklist(
            new Dictionary<string, string>(StringComparer.Ordinal),
            [],
            new Dictionary<string, ClassificacaoDoTipoDeAto>(StringComparer.Ordinal)
            {
                [codigo] = new(ehResultado, congela),
            });

    private static FaseCronograma FaseComProdutos(params ProdutoDaFase[] produtos) => FaseCronograma.Criar(
        1, Guid.CreateVersion7(), "RESULTADO_FINAL", OrigemDataFase.NaExecucao,
        agrupaEtapas: true, permiteComplementacao: false, coletaInscricao: true, coletaSolicitacaoIsencao: false,
        inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
        produtos: produtos,
        faseConcluinteCodigo: null, emiteParecerIndividual: false,
        bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;

    [Fact(DisplayName = "Tipo classificado como resultado declarado sem papel: item vermelho e a publicação recusa")]
    public void ResultadoSemPapel_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConforme(FaseComProdutos(
            ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo),
            ProdutoDaFase.Criar("GABARITO", papel: null)));
        TiposDeAtoUnicosPorObjeto tipos = Catalogo("GABARITO", ehResultado: true, congela: false);

        Vermelhos(processo, ContextoDeContagemDePrazos.SemCalendario, tipos).Should().Equal("cronograma_resultado_declarado_com_papel");

        Result<VersaoConfiguracao> publicar = Publicar(processo, ContextoDeContagemDePrazos.SemCalendario, tipos);

        publicar.IsFailure.Should().BeTrue();
        publicar.Error!.Code.Should().Be("ProcessoSeletivo.ResultadoDeclaradoSemPapel");
    }

    [Fact(DisplayName = "Tipo que congela a configuração declarado como item de resultado: item vermelho e a publicação recusa")]
    public void TipoQueCongelaComoResultado_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConforme(FaseComProdutos(ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)));
        TiposDeAtoUnicosPorObjeto tipos = Catalogo("RESULTADO_CLASSIFICACAO", ehResultado: true, congela: true);

        Vermelhos(processo, ContextoDeContagemDePrazos.SemCalendario, tipos).Should().Equal("cronograma_tipo_que_congela_nao_eh_resultado");

        Result<VersaoConfiguracao> publicar = Publicar(processo, ContextoDeContagemDePrazos.SemCalendario, tipos);

        publicar.IsFailure.Should().BeTrue();
        publicar.Error!.Code.Should().Be("ProcessoSeletivo.TipoQueCongelaDeclaradoComoResultado");
    }

    [Fact(DisplayName = "Tipo que não é resultado, sem papel, e tipo desconhecido do catálogo não são recusados")]
    public void TipoQueNaoEhResultadoSemPapel_Passa()
    {
        ProcessoSeletivo processo = ProcessoConforme(FaseComProdutos(
            ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo),
            ProdutoDaFase.Criar("COMUNICADO", papel: null)));

        Vermelhos(processo, ContextoDeContagemDePrazos.SemCalendario, Catalogo("COMUNICADO", ehResultado: false, congela: false))
            .Should().BeEmpty();
        Vermelhos(processo, ContextoDeContagemDePrazos.SemCalendario).Should().BeEmpty();
    }
}
