namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Um processo em rascunho com duas fases e duas etapas, cuja segunda fase e cujo segundo recurso da segunda etapa citam a regra do
/// catálogo com conteúdo diferente do atual, e por isso a publicação os recusa. Depois da recusa, <see cref="OutraPessoaGravaAsync"/>
/// faz o que outra pessoa faria ao editar o mesmo processo: troca a ordem das fases e das etapas e grava de novo os recursos da etapa.
/// </summary>
internal sealed class ProcessoComListasReordenaveis
{
    /// <summary>A fase que cita a regra de recurso com conteúdo diferente do catálogo; é a segunda do cronograma.</summary>
    internal const string CodigoDaFaseComRecurso = "CLASSIFICACAO";

    /// <summary>A etapa cujo recurso cita a regra com conteúdo diferente do catálogo; é a segunda das etapas.</summary>
    internal const string NomeDaEtapaComRecursos = "Prova Discursiva";

    private const string CodigoDaFaseDeResultado = "RESULTADO_FINAL";

    private static readonly string HashDiferenteDoCatalogo = new('0', 64);

    private readonly CascadingFixture _fixture;
    private readonly ReferenciaRegra _regraDoCatalogo;
    private readonly Guid _origemDaFaseDeResultado = Guid.CreateVersion7();
    private readonly Guid _origemDaFaseComRecurso = Guid.CreateVersion7();

    private ProcessoComListasReordenaveis(CascadingFixture fixture, ReferenciaRegra regraDoCatalogo)
    {
        _fixture = fixture;
        _regraDoCatalogo = regraDoCatalogo;
    }

    public Guid ProcessoId { get; private set; }

    public Guid DocumentoId { get; private set; }

    public Guid EtapaId { get; private set; }

    /// <summary>A identidade, antes da nova gravação, do recurso da etapa que cita a regra com conteúdo diferente do catálogo.</summary>
    public Guid RecursoDaCienciaId { get; private set; }

    public static async Task<ProcessoComListasReordenaveis> SemearAsync(CascadingFixture fixture, string cenario, ReferenciaRegra regraDoCatalogo)
    {
        ProcessoComListasReordenaveis semeado = new(fixture, regraDoCatalogo);
        await TiposDeAtoSeeder.SemearAsync(fixture.Factory.Services);
        await using AsyncServiceScope escopo = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, semeado.Opcoes(cenario));
        semeado.Registrar(processo, documento);

        return semeado;
    }

    /// <summary>Troca a ordem das duas fases e das duas etapas e grava de novo os recursos da segunda etapa, que ganham identidade nova.</summary>
    public async Task OutraPessoaGravaAsync()
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        ProcessoSeletivo processo = (await ProcessoSeletivoRepositoryDeTeste.Criar(db).ObterComConfiguracaoAsync(ProcessoId))!;

        ReordenarAsFases(processo);
        ReordenarAsEtapas(processo);
        RegravarOsRecursosDaEtapa(processo);
        await db.SaveChangesAsync();
    }

    private OpcoesDoProcessoSeletivoPublicavel Opcoes(string cenario) => new()
    {
        Nome = $"{cenario} {Guid.CreateVersion7()}",
        OrigemDaFaseDeResultado = _origemDaFaseDeResultado,
        FasesAdicionais = [FaseComRecursoDivergente(ordem: 2)],
        EtapasAdicionais = [EtapaComRecursosDivergentes()],
    };

    private void Registrar(ProcessoSeletivo processo, DocumentoAPublicar documento)
    {
        EtapaProcesso etapa = processo.Etapas.Single(e => e.Nome == NomeDaEtapaComRecursos);

        ProcessoId = processo.Id;
        DocumentoId = documento.Id;
        EtapaId = etapa.Id;
        RecursoDaCienciaId = etapa.Recursos.Single(r => r.Ancora == AncoraDoRecurso.CienciaIndividual).Id;
    }

    // As duas fases trocam de posição por uma cadeia aberta (a de resultado vai para uma ordem livre e a outra ocupa a que ficou
    // vaga): a troca direta entre duas ordens já ocupadas o cronograma recusa.
    private void ReordenarAsFases(ProcessoSeletivo processo)
    {
        FaseCronograma deResultado = ProcessoSeletivoPublicavelSeeder.FaseDeResultadoFinal(
            new OpcoesDoProcessoSeletivoPublicavel { Nome = "reordenação", OrdemDaFaseDeResultado = 3, OrigemDaFaseDeResultado = _origemDaFaseDeResultado });

        processo.DefinirCronogramaFases([deResultado, FaseComRecursoDivergente(ordem: 1)], []).IsSuccess.Should().BeTrue();
    }

    private static void ReordenarAsEtapas(ProcessoSeletivo processo)
    {
        MoverParaAOrdem(processo.Etapas.Single(e => e.Ordem == 1), ordem: 3);
        MoverParaAOrdem(processo.Etapas.Single(e => e.Ordem == 2), ordem: 1);
    }

    private static void MoverParaAOrdem(EtapaProcesso etapa, int ordem) => etapa.AtualizarDados(
        new IdentificacaoDaEtapa(etapa.Nome, etapa.Carater, etapa.TipoEtapa, ordem, etapa.FaseCodigo),
        new DeclaracaoDaAvaliacaoDaEtapa(
            etapa.Peso, etapa.NotaMinima, etapa.NotaMaxima, etapa.PassoDoLancamento, etapa.FormaDeAvaliacao, etapa.RegraConsolidacao, etapa.EscalaDeAtendimento))
        .IsSuccess.Should().BeTrue();

    private void RegravarOsRecursosDaEtapa(ProcessoSeletivo processo)
    {
        EtapaProcesso etapa = processo.Etapas.Single(e => e.Nome == NomeDaEtapaComRecursos);

        etapa.DefinirRecursos(RecursosDivergentesDaEtapa(etapa)).IsSuccess.Should().BeTrue();
    }

    // Preliminar e definitivo do mesmo ato, e o prazo de recurso ancorado no preliminar, com a regra citada com conteúdo diferente do catálogo.
    private FaseCronograma FaseComRecursoDivergente(int ordem)
    {
        ProdutoDaFase preliminar = ProdutoDaFase.Criar(DadosDoAtoDeTeste.TipoResultado, PapelProdutoFase.Preliminar);
        ProdutoDaFase definitivo = ProdutoDaFase.Criar(DadosDoAtoDeTeste.TipoResultado, PapelProdutoFase.Definitivo);
        RegraRecursoFase recurso = RegraRecursoFase.Criar(
            ReferenciaRegra.De(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", HashDiferenteDoCatalogo),
            ParametrosDoRecursoDeTeste.Declarados(48m, UnidadePrazo.Horas),
            preliminar.Id).Value!;

        return FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            new OrigemCanonicaDaFase(_origemDaFaseComRecurso, CodigoDaFaseComRecurso, OrigemDataFase.NoEdital, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(ordem, new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 2, 10, 0, 0, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase([preliminar, definitivo], EmiteParecerIndividual: false, recurso),
            bancasRequeridas: []).Value!;
    }

    private EtapaProcesso EtapaComRecursosDivergentes()
    {
        EtapaProcesso etapa = EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
            new IdentificacaoDaEtapa(
                NomeDaEtapaComRecursos, CaraterEtapa.Classificatoria,
                TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_DISCURSIVA", "Prova Discursiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!,
                Ordem: 2, CodigoDaFaseDeResultado),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
        etapa.DefinirJanelaEParecer(null, null, emiteParecerIndividual: true).IsSuccess.Should().BeTrue();
        etapa.DefinirProdutos([ProdutoDaEtapa.Criar("GABARITO", PapelProdutoFase.Preliminar)]).IsSuccess.Should().BeTrue();
        etapa.DefinirRecursos(RecursosDivergentesDaEtapa(etapa)).IsSuccess.Should().BeTrue();

        return etapa;
    }

    // Declarados na ordem inversa da canônica (a ciência individual vem depois do ato publicado): só a ciência cita a regra com
    // conteúdo diferente do catálogo, e a posição dela na lista da etapa difere da posição em que foi declarada.
    private List<RecursoDaEtapa> RecursosDivergentesDaEtapa(EtapaProcesso etapa) =>
    [
        RecursoDaEtapaDe(AncoraDoRecurso.CienciaIndividual, ReferenciaRegra.De(_regraDoCatalogo.Codigo, _regraDoCatalogo.Versao, HashDiferenteDoCatalogo), Guid.Empty),
        RecursoDaEtapaDe(AncoraDoRecurso.AtoPublicado, _regraDoCatalogo, etapa.Produtos.Single().Id),
    ];

    private static RecursoDaEtapa RecursoDaEtapaDe(AncoraDoRecurso ancora, ReferenciaRegra regra, Guid produtoAncoraId) =>
        RecursoDaEtapa.Criar(ancora, regra, ParametrosDoRecursoDeTeste.Declarados(48m, UnidadePrazo.Horas), produtoAncoraId).Value!;
}
