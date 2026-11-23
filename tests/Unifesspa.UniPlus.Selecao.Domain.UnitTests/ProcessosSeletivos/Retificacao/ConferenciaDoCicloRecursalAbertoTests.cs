namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Retificacao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A retificação não desfaz o ciclo de recurso que o preliminar publicado abriu: enquanto o definitivo da
/// matéria não saiu, o preliminar e o definitivo continuam no cronograma, ainda fechando o mesmo ciclo.
/// </summary>
public sealed class ConferenciaDoCicloRecursalAbertoTests
{
    private const string Materia = "RESULTADO_PROVA";
    private const string OutraMateria = "RESULTADO_ANALISE_CURRICULAR";
    private static readonly DateTimeOffset Agora = new(2027, 1, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly Dictionary<string, Guid> _origens = [];

    [Fact(DisplayName = "Preliminar publicado e retificação que retira o definitivo da fase é recusada, nomeando a matéria no campo e na mensagem")]
    public void PreliminarPublicado_RetiraODefinitivo_ERecusada()
    {
        ProdutoDaFase preliminar = Produto(Materia, PapelProdutoFase.Preliminar);
        FaseCronograma publicada = Fase("PROVA", 1, preliminar, Produto(Materia, PapelProdutoFase.Definitivo));

        IReadOnlyList<FieldError> recusas = Conferir(
            publicada: [publicada],
            nova: [Fase("PROVA", 1, preliminar)],
            publicados: [Item(publicada, preliminar)]);

        FieldError recusa = recusas.Should().ContainSingle().Subject;
        recusa.Field.Should().Be("cronograma.fases");
        recusa.Error.Code.Should().Be(ProcessoSeletivoErrorCodes.RetificacaoDesfazCicloRecursalAberto);
        recusa.Error.Message.Should().Contain(Materia).And.Contain("Publique o definitivo antes de retificar");
    }

    [Fact(DisplayName = "Preliminar publicado e retificação que leva o definitivo para outra fase é recusada")]
    public void PreliminarPublicado_MoveODefinitivoParaOutraFase_ERecusada()
    {
        ProdutoDaFase preliminar = Produto(Materia, PapelProdutoFase.Preliminar);
        FaseCronograma prova = Fase("PROVA", 1, preliminar, Produto(Materia, PapelProdutoFase.Definitivo));

        IReadOnlyList<FieldError> recusas = Conferir(
            publicada: [prova, Fase("RESULTADO", 2)],
            nova: [Fase("PROVA", 1, preliminar), Fase("RESULTADO", 2, Produto(Materia, PapelProdutoFase.Definitivo))],
            publicados: [Item(prova, preliminar)]);

        recusas.Should().ContainSingle().Which.Error.Code.Should().Be(ProcessoSeletivoErrorCodes.RetificacaoDesfazCicloRecursalAberto);
    }

    [Fact(DisplayName = "Com o definitivo já publicado a retificação que retira os dois itens não é recusada")]
    public void DefinitivoPublicado_RetiraOsItens_NaoRecusa()
    {
        ProdutoDaFase preliminar = Produto(Materia, PapelProdutoFase.Preliminar);
        ProdutoDaFase definitivo = Produto(Materia, PapelProdutoFase.Definitivo);
        FaseCronograma publicada = Fase("PROVA", 1, preliminar, definitivo);

        Conferir(
            publicada: [publicada],
            nova: [Fase("PROVA", 1)],
            publicados: [Item(publicada, preliminar), Item(publicada, definitivo)]).Should().BeEmpty();
    }

    [Fact(DisplayName = "Preliminar publicado sem definitivo no cronograma e retificação que o retira é recusada")]
    public void PreliminarPublicadoSemDefinitivo_RetiraOPreliminar_Recusa()
    {
        FaseCronograma publicada = Fase("PROVA", 1, Produto(Materia, PapelProdutoFase.Preliminar));

        IReadOnlyList<FieldError> recusas = Conferir(
            publicada: [publicada], nova: [Fase("PROVA", 1)], publicados: [Item(publicada, publicada.Produtos.Single())]);

        recusas.Should().ContainSingle().Which.Field.Should().Be("cronograma.fases");
    }

    [Fact(DisplayName = "Com o preliminar não publicado a retificação que retira o par não é recusada")]
    public void PreliminarNaoPublicado_RetiraOPar_NaoRecusa()
    {
        FaseCronograma publicada = Fase("PROVA", 1, Produto(Materia, PapelProdutoFase.Preliminar), Produto(Materia, PapelProdutoFase.Definitivo));

        Conferir(publicada: [publicada], nova: [Fase("PROVA", 1)], publicados: []).Should().BeEmpty();
    }

    [Fact(DisplayName = "Preliminar publicado e retificação que mantém o par no mesmo ciclo não é recusada")]
    public void PreliminarPublicado_MantemOPar_NaoRecusa()
    {
        ProdutoDaFase preliminar = Produto(Materia, PapelProdutoFase.Preliminar);
        ProdutoDaFase definitivo = Produto(Materia, PapelProdutoFase.Definitivo);
        FaseCronograma publicada = Fase("PROVA", 1, preliminar, definitivo);

        Conferir(
            publicada: [publicada],
            nova: [Fase("PROVA", 1, preliminar, definitivo)],
            publicados: [Item(publicada, preliminar)]).Should().BeEmpty();
    }

    [Fact(DisplayName = "Preliminar publicado e cronograma novo com os mesmos itens, de identificadores diferentes, não é recusado")]
    public void PreliminarPublicado_ItensComIdentificadoresNovosNoMesmoLugar_NaoRecusa()
    {
        ProdutoDaFase preliminar = Produto(Materia, PapelProdutoFase.Preliminar);
        FaseCronograma publicada = Fase("PROVA", 1, preliminar, Produto(Materia, PapelProdutoFase.Definitivo));

        Conferir(
            publicada: [publicada],
            nova: [Fase("PROVA", 2, Produto(Materia, PapelProdutoFase.Preliminar), Produto(Materia, PapelProdutoFase.Definitivo))],
            publicados: [Item(publicada, preliminar)]).Should().BeEmpty();
    }

    [Fact(DisplayName = "Preliminar publicado e fase deslocada com produtos de Id novo, sem o definitivo, é recusada")]
    public void PreliminarPublicado_FaseDeslocadaComProdutosRecriados_RetiraODefinitivo_ERecusada()
    {
        ProdutoDaFase preliminar = Produto(Materia, PapelProdutoFase.Preliminar);
        FaseCronograma publicada = Fase("PROVA", 1, preliminar, Produto(Materia, PapelProdutoFase.Definitivo));

        IReadOnlyList<FieldError> recusas = Conferir(
            publicada: [publicada],
            nova: [Fase("HOMOLOGACAO_INSCRICOES", 1), Fase("PROVA", 2, Produto(Materia, PapelProdutoFase.Preliminar))],
            publicados: [Item(publicada, preliminar)]);

        recusas.Should().ContainSingle().Which.Error.Code.Should().Be(ProcessoSeletivoErrorCodes.RetificacaoDesfazCicloRecursalAberto);
    }

    [Fact(DisplayName = "Preliminar publicado numa etapa renomeada, com o mesmo par, não é recusado")]
    public void PreliminarPublicado_EtapaRenomeada_NaoRecusa()
    {
        Guid etapaId = Guid.CreateVersion7();
        ProdutoDaEtapa preliminar = ProdutoDaEtapa.Criar(Materia, PapelProdutoFase.Preliminar);
        EtapaProcesso publicada = Etapa(etapaId, "Prova objetiva", preliminar, ProdutoDaEtapa.Criar(Materia, PapelProdutoFase.Definitivo));
        EtapaProcesso nova = Etapa(etapaId, "Prova objetiva (reaplicação)", ProdutoDaEtapa.Criar(Materia, PapelProdutoFase.Preliminar), ProdutoDaEtapa.Criar(Materia, PapelProdutoFase.Definitivo));

        ConferenciaDoCicloRecursalAberto
            .Conferir([], [publicada], [], [nova], Publicados(IdentidadeDoItemDoEdital.DaEtapa(publicada, preliminar)))
            .Should().BeEmpty();
    }

    [Fact(DisplayName = "Preliminar publicado e retirada do par de uma de duas etapas de mesmo nome é recusada")]
    public void PreliminarPublicado_RetiraOParDeUmaDeDuasEtapasHomonimas_ERecusada()
    {
        Guid primeira = Guid.CreateVersion7();
        Guid segunda = Guid.CreateVersion7();
        ProdutoDaEtapa preliminar = ProdutoDaEtapa.Criar(Materia, PapelProdutoFase.Preliminar);
        EtapaProcesso primeiraPublicada = Etapa(primeira, "Prova", preliminar, ProdutoDaEtapa.Criar(Materia, PapelProdutoFase.Definitivo));
        EtapaProcesso segundaPublicada = EtapaComPar(segunda, "Prova");

        IReadOnlyList<FieldError> recusas = ConferenciaDoCicloRecursalAberto.Conferir(
            [], [primeiraPublicada, segundaPublicada],
            [], [Etapa(primeira, "Prova"), EtapaComPar(segunda, "Prova")],
            Publicados(IdentidadeDoItemDoEdital.DaEtapa(primeiraPublicada, preliminar)));

        recusas.Should().ContainSingle().Which.Error.Code.Should().Be(ProcessoSeletivoErrorCodes.RetificacaoDesfazCicloRecursalAberto);
    }

    [Fact(DisplayName = "Duas matérias desfeitas geram duas recusas, em ordem ordinal do código, cada uma nomeando a própria matéria")]
    public void DuasMateriasDesfeitas_GeramDuasRecusasEmOrdem()
    {
        ProdutoDaFase preliminarDaProva = Produto(Materia, PapelProdutoFase.Preliminar);
        ProdutoDaFase preliminarDaAnalise = Produto(OutraMateria, PapelProdutoFase.Preliminar);
        FaseCronograma publicada = Fase(
            "PROVA", 1, preliminarDaProva, Produto(Materia, PapelProdutoFase.Definitivo), preliminarDaAnalise, Produto(OutraMateria, PapelProdutoFase.Definitivo));

        IReadOnlyList<FieldError> recusas = Conferir(
            publicada: [publicada],
            nova: [Fase("PROVA", 1)],
            publicados: [Item(publicada, preliminarDaProva), Item(publicada, preliminarDaAnalise)]);

        recusas.Select(static r => r.Field).Should().AllBe("cronograma.fases");
        recusas.Select(static r => r.Error.Message).Should().SatisfyRespectively(
            static primeira => primeira.Should().Contain($"'{OutraMateria}'"), static segunda => segunda.Should().Contain($"'{Materia}'"));
    }

    [Fact(DisplayName = "A mesma matéria desfeita em dois lugares gera uma só recusa")]
    public void MesmaMateriaDesfeitaEmDoisLugares_GeraUmaRecusa()
    {
        ProdutoDaFase preliminarDaProva = Produto(Materia, PapelProdutoFase.Preliminar);
        ProdutoDaFase preliminarDaReaplicacao = Produto(Materia, PapelProdutoFase.Preliminar);
        FaseCronograma prova = Fase("PROVA", 1, preliminarDaProva, Produto(Materia, PapelProdutoFase.Definitivo));
        FaseCronograma reaplicacao = Fase("REAPLICACAO", 2, preliminarDaReaplicacao, Produto(Materia, PapelProdutoFase.Definitivo));

        IReadOnlyList<FieldError> recusas = Conferir(
            publicada: [prova, reaplicacao],
            nova: [Fase("PROVA", 1), Fase("REAPLICACAO", 2)],
            publicados: [Item(prova, preliminarDaProva), Item(reaplicacao, preliminarDaReaplicacao)]);

        recusas.Should().ContainSingle().Which.Field.Should().Be("cronograma.fases");
    }

    [Fact(DisplayName = "Só a matéria cujo ciclo ficou aberto é recusada quando a outra já tem o definitivo publicado")]
    public void UmaMateriaFechadaEOutraAberta_RecusaSoAAberta()
    {
        ProdutoDaFase preliminarDaProva = Produto(Materia, PapelProdutoFase.Preliminar);
        ProdutoDaFase definitivoDaProva = Produto(Materia, PapelProdutoFase.Definitivo);
        ProdutoDaFase preliminarDaAnalise = Produto(OutraMateria, PapelProdutoFase.Preliminar);
        FaseCronograma publicada = Fase(
            "PROVA", 1, preliminarDaProva, definitivoDaProva, preliminarDaAnalise, Produto(OutraMateria, PapelProdutoFase.Definitivo));

        IReadOnlyList<FieldError> recusas = Conferir(
            publicada: [publicada],
            nova: [Fase("PROVA", 1)],
            publicados: [Item(publicada, preliminarDaProva), Item(publicada, definitivoDaProva), Item(publicada, preliminarDaAnalise)]);

        recusas.Should().ContainSingle().Which.Field.Should().Be("cronograma.fases");
    }

    private static HashSet<IdentidadeDoItemDoEdital> Publicados(params IdentidadeDoItemDoEdital[] itens) => [.. itens];

    private static IdentidadeDoItemDoEdital Item(FaseCronograma fase, ProdutoDaFase produto) => IdentidadeDoItemDoEdital.DaFase(fase, produto);

    private static IReadOnlyList<FieldError> Conferir(FaseCronograma[] publicada, FaseCronograma[] nova, IdentidadeDoItemDoEdital[] publicados) =>
        ConferenciaDoCicloRecursalAberto.Conferir(publicada, [], nova, [], Publicados(publicados));

    private static ProdutoDaFase Produto(string materia, PapelProdutoFase papel) => ProdutoDaFase.Criar(materia, papel);

    // A mesma fase, em versões diferentes do cronograma, tem a mesma fase canônica de origem.
    private FaseCronograma Fase(string codigo, int ordem, params ProdutoDaFase[] produtos) =>
        FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            new OrigemCanonicaDaFase(OrigemDe(codigo), codigo, OrigemDataFase.NoEdital, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(ordem, Inicio: Agora, Fim: Agora.AddDays(10), UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase(Produtos: produtos, EmiteParecerIndividual: false, RegraRecurso: null),
            bancasRequeridas: []).Value!;

    private Guid OrigemDe(string codigo)
    {
        if (!_origens.TryGetValue(codigo, out Guid origem))
        {
            origem = Guid.CreateVersion7();
            _origens[codigo] = origem;
        }

        return origem;
    }

    private static EtapaProcesso EtapaComPar(Guid id, string nome) =>
        Etapa(id, nome, ProdutoDaEtapa.Criar(Materia, PapelProdutoFase.Preliminar), ProdutoDaEtapa.Criar(Materia, PapelProdutoFase.Definitivo));

    private static EtapaProcesso Etapa(Guid id, string nome, params ProdutoDaEtapa[] produtos)
    {
        EtapaProcesso etapa = EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
            new IdentificacaoDaEtapa(Nome: nome, Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
        typeof(EtapaProcesso).GetProperty(nameof(EtapaProcesso.Id))!.SetValue(etapa, id);
        etapa.DefinirProdutos(produtos).IsSuccess.Should().BeTrue();
        return etapa;
    }
}
