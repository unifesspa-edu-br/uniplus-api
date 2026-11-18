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
/// tipo de ato no mesmo lugar e os que as fases de origem deixam abertos para a concluinte, e a pergunta
/// inversa responde com os mesmos pares.
/// </summary>
public sealed class PreliminaresDaMateriaTests
{
    private const string Materia = "RESULTADO_PROVA";
    private const string OutraMateria = "GABARITO";
    private static readonly DateTimeOffset Inicio = new(2027, 1, 20, 12, 0, 0, TimeSpan.Zero);

    private static ProdutoDaFase Preliminar(string ato = Materia) => ProdutoDaFase.Criar(ato, PapelProdutoFase.Preliminar);

    private static ProdutoDaFase Definitivo(string ato = Materia) => ProdutoDaFase.Criar(ato, PapelProdutoFase.Definitivo);

    private static FaseCronograma Fase(string codigo, string? faseConcluinteCodigo, params ProdutoDaFase[] produtos) =>
        FaseCronograma.Criar(
            new OrigemCanonicaDaFase(Guid.CreateVersion7(), codigo, OrigemDataFase.NoEdital, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(1, Inicio: Inicio, Fim: Inicio.AddDays(10), FaseConcluinteCodigo: faseConcluinteCodigo, UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase(Produtos: produtos, EmiteParecerIndividual: false, RegraRecurso: null),
            bancasRequeridas: []).Value!;

    private static EtapaProcesso Etapa(params ProdutoDaEtapa[] produtos)
    {
        EtapaProcesso etapa = EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(Nome: "Prova objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoDaEtapa(), Ordem: null, FaseCodigo: null),
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
        FaseCronograma fase = Fase("PROVA", null, preliminar, Preliminar(OutraMateria), definitivo);

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

    [Fact(DisplayName = "Fase concluinte recebe o ciclo que a fase de origem deixa aberto")]
    public void Esperados_FaseConcluinte_RecebeOCicloAbertoPelaOutraFase()
    {
        ProdutoDaFase preliminar = Preliminar();
        ProdutoDaFase definitivo = Definitivo();
        FaseCronograma prova = Fase("PROVA", "RESULTADO", preliminar, Preliminar(OutraMateria), Definitivo(OutraMateria));
        FaseCronograma resultado = Fase("RESULTADO", null, definitivo);

        PreliminaresDaMateria.Esperados([prova, resultado], [], definitivo.Id).Should().Equal(preliminar.Id);
    }

    [Fact(DisplayName = "Duas fases com o mesmo tipo e a mesma concluinte fazem o definitivo da concluinte esperar os dois")]
    public void Esperados_DuasFasesComAMesmaConcluinte_EsperaOsDois()
    {
        ProdutoDaFase primeiro = Preliminar();
        ProdutoDaFase segundo = Preliminar();
        ProdutoDaFase definitivo = Definitivo();
        FaseCronograma[] fases = [Fase("PROVA_A", "RESULTADO", primeiro), Fase("PROVA_B", "RESULTADO", segundo), Fase("RESULTADO", null, definitivo)];

        PreliminaresDaMateria.Esperados(fases, [], definitivo.Id).Should().BeEquivalentTo([primeiro.Id, segundo.Id]);
    }

    [Fact(DisplayName = "A matéria que a fase fecha com o próprio definitivo não chega à concluinte")]
    public void Esperados_FaseQueFechaOCiclo_NaoEntregaAConcluinte()
    {
        ProdutoDaFase preliminar = Preliminar();
        ProdutoDaFase definitivoDaProva = Definitivo();
        ProdutoDaFase definitivoDoResultado = Definitivo();
        FaseCronograma[] fases = [Fase("PROVA", "RESULTADO", preliminar, definitivoDaProva), Fase("RESULTADO", null, definitivoDoResultado)];

        PreliminaresDaMateria.Esperados(fases, [], definitivoDoResultado.Id).Should().BeEmpty();
        PreliminaresDaMateria.Esperados(fases, [], definitivoDaProva.Id).Should().Equal(preliminar.Id);
    }

    [Fact(DisplayName = "Ciclo aberto por fase que aponta outra concluinte não chega ao definitivo")]
    public void Esperados_ConcluinteDiferente_NaoEspera()
    {
        ProdutoDaFase definitivo = Definitivo();
        FaseCronograma[] fases = [Fase("PROVA", "RECURSOS", Preliminar()), Fase("RESULTADO", null, definitivo), Fase("RECURSOS", null)];

        PreliminaresDaMateria.Esperados(fases, [], definitivo.Id).Should().BeEmpty();
    }

    [Fact(DisplayName = "Preliminar de outro tipo de ato aberto pela fase de origem não chega ao definitivo")]
    public void Esperados_CicloAbertoDeOutroTipo_NaoEspera()
    {
        ProdutoDaFase definitivo = Definitivo();
        FaseCronograma[] fases = [Fase("PROVA", "RESULTADO", Preliminar(OutraMateria)), Fase("RESULTADO", null, definitivo)];

        PreliminaresDaMateria.Esperados(fases, [], definitivo.Id).Should().BeEmpty();
    }

    [Fact(DisplayName = "Produto sem preliminar declarado, ou que o cronograma não conhece, não espera nada")]
    public void Esperados_SemPreliminarDeclarado_EVazio()
    {
        ProdutoDaFase definitivo = Definitivo();
        FaseCronograma fase = Fase("RESULTADO", null, definitivo);

        PreliminaresDaMateria.Esperados([fase], [], definitivo.Id).Should().BeEmpty();
        PreliminaresDaMateria.Esperados([fase], [], Guid.CreateVersion7()).Should().BeEmpty();
    }

    [Fact(DisplayName = "A configuração publicada responde às duas perguntas como o cronograma solto")]
    public void ConfiguracaoPublicada_RespondeComoOCronogramaSolto()
    {
        ProdutoDaFase preliminar = Preliminar();
        ProdutoDaFase definitivo = Definitivo();
        ConfiguracaoPublicada configuracao = Configuracao([Fase("PROVA", "RESULTADO", preliminar), Fase("RESULTADO", null, definitivo)], []);

        PreliminaresDaMateria.Esperados(configuracao, definitivo.Id).Should().Equal(preliminar.Id);
        PreliminaresDaMateria.DefinitivosQueFecham(configuracao, preliminar.Id).Should().Equal(definitivo.Id);
    }

    [Fact(DisplayName = "O preliminar é fechado pelo definitivo da própria fase")]
    public void DefinitivosQueFecham_PreliminarDaFase_ApontaODefinitivoDaFase()
    {
        ProdutoDaFase preliminar = Preliminar();
        ProdutoDaFase definitivo = Definitivo();
        FaseCronograma fase = Fase("PROVA", null, preliminar, definitivo, Definitivo(OutraMateria));

        PreliminaresDaMateria.DefinitivosQueFecham([fase], [], preliminar.Id).Should().Equal(definitivo.Id);
    }

    [Fact(DisplayName = "O preliminar da etapa é fechado pelo definitivo da etapa")]
    public void DefinitivosQueFecham_PreliminarDaEtapa_ApontaODefinitivoDaEtapa()
    {
        ProdutoDaEtapa preliminar = ProdutoDaEtapa.Criar(Materia, PapelProdutoFase.Preliminar);
        ProdutoDaEtapa definitivo = ProdutoDaEtapa.Criar(Materia, PapelProdutoFase.Definitivo);

        PreliminaresDaMateria.DefinitivosQueFecham([], [Etapa(preliminar, definitivo)], preliminar.Id).Should().Equal(definitivo.Id);
    }

    [Fact(DisplayName = "O preliminar que a fase deixa aberto é fechado pelo definitivo da concluinte")]
    public void DefinitivosQueFecham_PreliminarAberto_ApontaODefinitivoDaConcluinte()
    {
        ProdutoDaFase preliminar = Preliminar();
        ProdutoDaFase definitivo = Definitivo();
        FaseCronograma[] fases = [Fase("PROVA", "RESULTADO", preliminar), Fase("RESULTADO", null, definitivo)];

        PreliminaresDaMateria.DefinitivosQueFecham(fases, [], preliminar.Id).Should().Equal(definitivo.Id);
    }

    [Fact(DisplayName = "O preliminar fechado na própria fase não aponta o definitivo da concluinte")]
    public void DefinitivosQueFecham_PreliminarFechadoNaFase_NaoApontaAConcluinte()
    {
        ProdutoDaFase preliminar = Preliminar();
        ProdutoDaFase definitivoDaProva = Definitivo();
        FaseCronograma[] fases = [Fase("PROVA", "RESULTADO", preliminar, definitivoDaProva), Fase("RESULTADO", null, Definitivo())];

        PreliminaresDaMateria.DefinitivosQueFecham(fases, [], preliminar.Id).Should().Equal(definitivoDaProva.Id);
    }

    [Fact(DisplayName = "Preliminar sem definitivo que o espere não é fechado por ninguém")]
    public void DefinitivosQueFecham_SemDefinitivo_EVazio()
    {
        ProdutoDaFase preliminar = Preliminar();
        FaseCronograma fase = Fase("PROVA", null, preliminar, Definitivo(OutraMateria));

        PreliminaresDaMateria.DefinitivosQueFecham([fase], [], preliminar.Id).Should().BeEmpty();
    }
}
