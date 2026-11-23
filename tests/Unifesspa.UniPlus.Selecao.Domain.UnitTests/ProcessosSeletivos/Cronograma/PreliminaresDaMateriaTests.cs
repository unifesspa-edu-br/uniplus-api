namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// Qual preliminar libera qual definitivo no cronograma: o definitivo espera os preliminares do mesmo
/// tipo de ato no mesmo lugar, e a pergunta inversa responde com os mesmos pares.
/// </summary>
public sealed class PreliminaresDaMateriaTests
{
    private const string Materia = "RESULTADO_PROVA";
    private const string OutraMateria = "GABARITO";
    private static readonly DateTimeOffset Inicio = new(2027, 1, 20, 12, 0, 0, TimeSpan.Zero);

    private static ProdutoDaFase Preliminar(string ato = Materia) => ProdutoDaFase.Criar(ato, PapelProdutoFase.Preliminar);

    private static ProdutoDaFase Definitivo(string ato = Materia) => ProdutoDaFase.Criar(ato, PapelProdutoFase.Definitivo);

    private static FaseCronograma Fase(string codigo, params ProdutoDaFase[] produtos) =>
        FaseCronograma.Criar(
            new OrigemCanonicaDaFase(Guid.CreateVersion7(), codigo, OrigemDataFase.NoEdital, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(1, Inicio: Inicio, Fim: Inicio.AddDays(10), UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase(Produtos: produtos, EmiteParecerIndividual: false, RegraRecurso: null),
            bancasRequeridas: []).Value!;

    private static EtapaProcesso Etapa(params ProdutoDaEtapa[] produtos)
    {
        EtapaProcesso etapa = EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(Nome: "Prova objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoDaEtapa(), Ordem: 1, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
        etapa.DefinirProdutos(produtos).IsSuccess.Should().BeTrue();

        return etapa;
    }

    private static TipoEtapaSnapshot TipoDaEtapa() => TipoEtapaSnapshot.Criar(
        Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!;

    private static ConfiguracaoPublicada Configuracao(FaseCronograma[] fases, EtapaProcesso[] etapas) => new ConfiguracaoPublicada(
            new CronogramaDeclarado(ConfiguracaoPublicadaDeTeste.Localidade, null, fases),
            new AvaliacaoDeclarada(etapas, [], Classificacao()),
            new VagasDeclaradas([], 1, false,
                null, null, AtendimentoEspecializado.Criar([], [], []).Value!),
            new ExigenciasDeclaradas([], []),
            new ColetaDeclarada([], [], [], [],
                [], [], null),
            new DivulgacaoETaxaDeclaradas(null, null), null);

    private static ConfiguracaoClassificacao Classificacao() => ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
    {
        RegraCalculo = ReferenciaRegra.De(RegraCalculoCodigo.ClassificacaoImportada, "v1", new string('b', 64)),
        RegraOrdemAlocacao = ReferenciaRegra.De(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "v1", new string('d', 64)),
    }).Value!;

    [Fact(DisplayName = "Definitivo da fase espera os preliminares do mesmo tipo de ato na própria fase")]
    public void Esperados_DefinitivoDaFase_EsperaOsPreliminaresDoMesmoTipo()
    {
        ProdutoDaFase preliminar = Preliminar();
        ProdutoDaFase definitivo = Definitivo();
        FaseCronograma fase = Fase("PROVA", preliminar, Preliminar(OutraMateria), definitivo);

        PreliminaresDaMateria.Esperados([fase], [], definitivo.Id).Should().Equal(preliminar.Id);
    }

    [Fact(DisplayName = "Definitivo da etapa espera os preliminares do mesmo tipo de ato na própria etapa")]
    public void Esperados_DefinitivoDaEtapa_EsperaOsPreliminaresDoMesmoTipo()
    {
        ProdutoDaEtapa preliminar = ProdutoDaEtapa.Criar(Materia, PapelProdutoFase.Preliminar);
        ProdutoDaEtapa definitivo = ProdutoDaEtapa.Criar(Materia, PapelProdutoFase.Definitivo);
        EtapaProcesso etapa = Etapa(preliminar, ProdutoDaEtapa.Criar(OutraMateria, PapelProdutoFase.Preliminar), definitivo);

        PreliminaresDaMateria.Esperados([], [etapa], definitivo.Id).Should().Equal(preliminar.Id);
    }

    [Fact(DisplayName = "Duas fases que publicam o mesmo tipo de ato não se ligam: o definitivo de uma não espera o preliminar da outra")]
    public void Esperados_MesmoTipoEmOutraFase_NaoEspera()
    {
        ProdutoDaFase preliminarDaProva = Preliminar();
        ProdutoDaFase definitivoDoResultado = Definitivo();
        FaseCronograma[] fases = [Fase("PROVA", preliminarDaProva), Fase("RESULTADO", definitivoDoResultado)];

        PreliminaresDaMateria.Esperados(fases, [], definitivoDoResultado.Id).Should().BeEmpty();
        PreliminaresDaMateria.DefinitivosQueFecham(fases, [], preliminarDaProva.Id).Should().BeEmpty();
    }

    [Fact(DisplayName = "Produto sem preliminar declarado, ou que o cronograma não conhece, não espera nada")]
    public void Esperados_SemPreliminarDeclarado_EVazio()
    {
        ProdutoDaFase definitivo = Definitivo();
        FaseCronograma fase = Fase("RESULTADO", definitivo);

        PreliminaresDaMateria.Esperados([fase], [], definitivo.Id).Should().BeEmpty();
        PreliminaresDaMateria.Esperados([fase], [], Guid.CreateVersion7()).Should().BeEmpty();
    }

    [Fact(DisplayName = "A configuração publicada responde às duas perguntas como o cronograma solto")]
    public void ConfiguracaoPublicada_RespondeComoOCronogramaSolto()
    {
        ProdutoDaFase preliminar = Preliminar();
        ProdutoDaFase definitivo = Definitivo();
        ConfiguracaoPublicada configuracao = Configuracao([Fase("PROVA", preliminar, definitivo)], []);

        PreliminaresDaMateria.Esperados(configuracao, definitivo.Id).Should().Equal(preliminar.Id);
        PreliminaresDaMateria.DefinitivosQueFecham(configuracao, preliminar.Id).Should().Equal(definitivo.Id);
    }

    [Fact(DisplayName = "O preliminar é fechado pelo definitivo da própria fase")]
    public void DefinitivosQueFecham_PreliminarDaFase_ApontaODefinitivoDaFase()
    {
        ProdutoDaFase preliminar = Preliminar();
        ProdutoDaFase definitivo = Definitivo();
        FaseCronograma fase = Fase("PROVA", preliminar, definitivo, Definitivo(OutraMateria));

        PreliminaresDaMateria.DefinitivosQueFecham([fase], [], preliminar.Id).Should().Equal(definitivo.Id);
    }

    [Fact(DisplayName = "O preliminar da etapa é fechado pelo definitivo da etapa")]
    public void DefinitivosQueFecham_PreliminarDaEtapa_ApontaODefinitivoDaEtapa()
    {
        ProdutoDaEtapa preliminar = ProdutoDaEtapa.Criar(Materia, PapelProdutoFase.Preliminar);
        ProdutoDaEtapa definitivo = ProdutoDaEtapa.Criar(Materia, PapelProdutoFase.Definitivo);

        PreliminaresDaMateria.DefinitivosQueFecham([], [Etapa(preliminar, definitivo)], preliminar.Id).Should().Equal(definitivo.Id);
    }

    [Fact(DisplayName = "Preliminar sem definitivo que o espere não é fechado por ninguém")]
    public void DefinitivosQueFecham_SemDefinitivo_EVazio()
    {
        ProdutoDaFase preliminar = Preliminar();
        FaseCronograma fase = Fase("PROVA", preliminar, Definitivo(OutraMateria));

        PreliminaresDaMateria.DefinitivosQueFecham([fase], [], preliminar.Id).Should().BeEmpty();
    }
}
