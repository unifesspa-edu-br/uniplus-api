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
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioProcessoSeletivoRestaurarConfiguracao;

public sealed class ProcessoSeletivoRestaurarConfiguracaoEquivalenciaTests
{
    [Fact(DisplayName = "Restaurar mais critérios de desempate que o teto é recusado, como na gravação")]
    public void Restaurar_CriteriosAcimaDoTeto_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TiposDeProcessoDeTeste.PSIQ);
        Estado antes = Estado.De(processo);
        CriterioDesempate[] criterios = [.. Enumerable.Range(1, RegrasDoDesempate.CriteriosDesempateMaximo + 1).Select(static ordem =>
            CriterioDesempate.Criar(ordem, Regra(CriterioDesempateCodigo.MaiorIdade, 'f'), new ArgsDesempateMaiorIdade()).Value!)];

        Result resultado = processo.RestaurarConfiguracaoCongelada(VersaoDo(processo), Grafo(criterios: criterios));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.CriteriosDesempateEmExcesso");
        Estado.De(processo).Should().BeEquivalentTo(antes);
    }

    [Fact(DisplayName = "RestaurarConfiguracaoCongelada produz o mesmo resultado em processos de Tipo diferente com a mesma configuração (indistinguibilidade, #850)")]
    public void RestaurarConfiguracaoCongelada_TiposDiferentesMesmaConfiguracao_ResultadoIdentico()
    {
        ProcessoSeletivo psiq = ProcessoPublicado(TiposDeProcessoDeTeste.PSIQ);
        ProcessoSeletivo sisu = ProcessoPublicado(TiposDeProcessoDeTeste.SiSU);
        VersaoConfiguracao versaoPsiq = VersaoDo(psiq);
        VersaoConfiguracao versaoSisu = VersaoDo(sisu);

        RegraEliminacao EliminacaoEnem() => RegraEliminacao.Criar(
            Regra(RegraEliminacaoCodigo.ElimCorteEmArea, 'a'), new ArgsElimCorteEmArea("REDACAO", 400m)).Value!;

        ConfiguracaoClassificacao ClassificacaoEnemValida() => ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = Regra(RegraCalculoCodigo.FormulaMediaPonderada, 'b'),
            RegraArredondamento = Regra(RegraArredondamentoCodigo.PrecisaoTruncar, 'c'),
            CasasArredondamento = 2,
            RegraOrdemAlocacao = Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'd'),
            RegrasEliminacao = [EliminacaoEnem()],
            BaseadoEmEnem = true,
            ResolucaoPesoAreaEnem = QuadroPesoAreaEnemDeTeste.Resolucao,
            QuadroPesoAreaEnem = QuadroPesoAreaEnemDeTeste.Completo(),
            EscalaNotaFinal = 100m,
        }).Value!;

        GrafoConfiguracao GrafoComClassificacaoEnem() => new(
            etapas: [EtapaCongeladaDeProvaObjetiva(EtapaCongelada, "Prova", ordem: 1)],
            atendimentoEspecializado: AtendimentoEspecializado.Criar([], [], []).Value!,
            distribuicaoVagas: [Distribuicao()],
            bonusRegional: null,
            criteriosDesempate: [],
            classificacao: ClassificacaoEnemValida(),
            cronogramaFases: [FaseConforme()],
            documentosExigidos: [],
            nosExigencia: [],
            referenciaTemporalFatos: null,
            numeroDeOpcoesDeCurso: 1);

        Result resultadoPsiq = psiq.RestaurarConfiguracaoCongelada(versaoPsiq, GrafoComClassificacaoEnem());
        Result resultadoSisu = sisu.RestaurarConfiguracaoCongelada(versaoSisu, GrafoComClassificacaoEnem());

        resultadoPsiq.IsSuccess.Should().BeTrue(resultadoPsiq.Error?.Message);
        resultadoSisu.IsSuccess.Should().Be(resultadoPsiq.IsSuccess,
            "o rótulo TipoProcesso não pode decidir o resultado da restauração — só BaseadoEmEnem, dado igual nos dois processos");
    }

    [Fact(DisplayName = "EtapaRef órfão também falha sem tocar no estado")]
    public void EtapaRefOrfao_NaoAlteraEstado()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TiposDeProcessoDeTeste.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);
        Estado antes = Estado.De(processo);

        GrafoConfiguracao invalido = Grafo(
            etapas: [EtapaCongeladaDeProvaObjetiva(EtapaCongelada, "Prova", ordem: 1)],
            criterios: [
                CriterioDesempate.Criar(
                    1,
                    Regra(CriterioDesempateCodigo.MaiorNotaEtapa, 'd'),
                    // Aponta para uma etapa que NÃO está no grafo — é o que aconteceria se o
                    // decoder regenerasse o etapa.Id em vez de preservá-lo.
                    new ArgsDesempateMaiorNotaEtapa(Guid.NewGuid())).Value!,
            ]);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, invalido);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.EtapaRefDesempateInexistente");
        Estado.De(processo).Should().BeEquivalentTo(antes);
    }

    [Fact(DisplayName = "Ids de etapa duplicados no grafo são recusados — a entidade não os validava")]
    public void IdsDeEtapaDuplicados_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TiposDeProcessoDeTeste.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);

        GrafoConfiguracao invalido = Grafo(etapas: [
            EtapaCongeladaDeProvaObjetiva(EtapaCongelada, "Prova A", ordem: 1),
            EtapaCongeladaDeProvaObjetiva(EtapaCongelada, "Prova B", ordem: 2, peso: 2m),
        ]);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, invalido);

        resultado.IsFailure.Should().BeTrue(
            "duas etapas com o mesmo Id são indistinguíveis para o etapa_ref, e o INSERT colidiria na chave " +
            "primária. A unicidade era garantida só pelo handler de PUT /etapas — a reposição não passa por ele.");
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.IdEtapaDuplicado");
    }

    [Fact(DisplayName = "Restaurar a configuração de OUTRO processo é recusado")]
    public void VersaoDeOutroProcesso_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TiposDeProcessoDeTeste.SiSU);
        ProcessoSeletivo alheio = ProcessoPublicado(TiposDeProcessoDeTeste.SiSU);

        Result resultado = processo.RestaurarConfiguracaoCongelada(VersaoDo(alheio), Grafo());

        resultado.IsFailure.Should().BeTrue(
            "repor num certame a configuração congelada de outro sobrescreveria o primeiro com uma configuração que " +
            "nunca foi dele — e a próxima publicação congelaria a troca");
        resultado.Error!.Code.Should().Be("VersaoConfiguracao.VersaoDeOutroProcesso");
    }

    [Fact(DisplayName = "Um processo em rascunho não tem configuração congelada a restaurar")]
    public void ProcessoEmRascunho_Recusa()
    {
        ProcessoSeletivo rascunho = ProcessoConforme(TiposDeProcessoDeTeste.SiSU);
        ProcessoSeletivo publicado = ProcessoPublicado(TiposDeProcessoDeTeste.SiSU);

        Result resultado = rascunho.RestaurarConfiguracaoCongelada(VersaoDo(publicado), Grafo());

        resultado.IsFailure.Should().BeTrue(
            "a reposição não é edição — ela devolve a configuração ao que a versão congelada já dizia. Num processo " +
            "que nunca publicou não há versão nenhuma, e a operação não tem sentido.");
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.RestauracaoForaDePublicado");
    }

    [Fact(DisplayName = "A etapa que sobrevive é RECONCILIADA na mesma instância (o CreatedAt não se perde)")]
    public void EtapaSobrevivente_EReconciliada()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TiposDeProcessoDeTeste.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);

        EtapaProcesso instanciaViva = processo.Etapas.Single();

        // A etapa congelada tem o MESMO Id da viva, mas dados diferentes.
        GrafoConfiguracao grafo = Grafo(etapas: [
            EtapaCongeladaDeProvaObjetiva(EtapaOriginal, "Nome Restaurado", ordem: 3, peso: 7m, notaMinima: 20m, carater: CaraterEtapa.Ambas),
        ]);

        processo.RestaurarConfiguracaoCongelada(versao, grafo).IsSuccess.Should().BeTrue();

        EtapaProcesso depois = processo.Etapas.Single();

        depois.Should().BeSameAs(instanciaViva,
            "substituir a instância tracked por outra com o mesmo Id colide com o identity map do EF — e o CreatedAt " +
            "original se perderia. A etapa é atualizada NA MESMA instância — o Id é preservado porque referências de " +
            "negócio (etapaRef) apontam para ele (ADR-0110).");
        depois.Nome.Should().Be("Nome Restaurado", "os dados vêm do grafo congelado, não da instância viva");
        depois.Peso.Should().Be(7m);
        depois.Ordem.Should().Be(3);
    }

    /// <summary>
    /// A fase em que a etapa acontece é dado congelado como qualquer outro. Sem repô-la, a etapa
    /// sobrevivente volta da restauração sem vínculo com o cronograma — e nenhum outro caminho a
    /// devolve, porque o vínculo só é declarado ao definir as etapas.
    /// </summary>
    [Fact(DisplayName = "A etapa que sobrevive recupera também a fase em que acontece")]
    public void EtapaSobrevivente_RecuperaAFaseCongelada()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TiposDeProcessoDeTeste.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);

        GrafoConfiguracao grafo = Grafo(etapas: [
            EtapaCongeladaDeProvaObjetiva(EtapaOriginal, "Prova", ordem: 1, faseCodigo: "RESULTADO_FINAL"),
        ]);

        processo.RestaurarConfiguracaoCongelada(versao, grafo).IsSuccess.Should().BeTrue();

        processo.Etapas.Single().FaseCodigo.Should().Be("RESULTADO_FINAL");
    }

    private static FaseCronograma FaseDeAvaliacao() => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            OrigemId: new Guid("eeee0000-0000-4000-8000-000000000002"), Codigo: "AVALIACAO", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false,
            ColetaInscricao: false, ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(
            Ordem: 2, Inicio: new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 2, 28, 0, 0, 0, TimeSpan.Zero), FaseConcluinteCodigo: null,
            UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    /// <summary>
    /// O descarte tem de devolver a etapa à fase que o envelope congelou, e não deixá-la na
    /// que a sessão editorial criou. Com o código como única representação do vínculo isso é
    /// consequência de repor o código — mas é justamente por ser consequência que convém
    /// afirmá-lo: o teste vigia o efeito, e não o mecanismo que o produz.
    /// </summary>
    [Fact(DisplayName = "A etapa restaurada volta presa à fase restaurada, e não à da sessão descartada")]
    public void EtapaSobrevivente_RecuperaOVinculoComAFaseCongelada()
    {
        // O estado de que o descarte parte: duas fases, e a etapa na segunda delas.
        FaseCronograma avaliacao = FaseDeAvaliacao();

        // O estado de que o descarte parte chega por uma restauração: o agregado publicado
        // recusa mudar de fase uma etapa fora de sessão editorial, e o que se testa aqui é o
        // vínculo que a reposição refaz, não o caminho que sujou a configuração.
        ProcessoSeletivo processo = ProcessoPublicado(TiposDeProcessoDeTeste.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);

        GrafoConfiguracao daSessao = Grafo(
            etapas: [
                EtapaCongeladaDeProvaObjetiva(EtapaOriginal, "Prova", ordem: 1, faseCodigo: "AVALIACAO"),
            ],
            // Só a fase nova: a bicondicional é por vínculo, e deixar no grafo uma fase que
            // agrupa etapas sem nenhuma etapa nela é estado que a restauração recusa — com
            // razão, e é outro teste.
            cronogramaFases: [avaliacao]);

        Result restauracaoDaSessao = processo.RestaurarConfiguracaoCongelada(versao, daSessao);
        restauracaoDaSessao.IsSuccess.Should().BeTrue(restauracaoDaSessao.Error?.Message);
        processo.EtapasDaFase("AVALIACAO").Should().ContainSingle(
            "pré-condição: a etapa está na fase que o descarte vai desfazer");

        GrafoConfiguracao grafo = Grafo(
            etapas: [
                EtapaCongeladaDeProvaObjetiva(EtapaOriginal, "Prova", ordem: 1, faseCodigo: "RESULTADO_FINAL"),
            ],
            cronogramaFases: [FaseConforme()]);

        processo.RestaurarConfiguracaoCongelada(versao, grafo).IsSuccess.Should().BeTrue();

        EtapaProcesso reposta = processo.Etapas.Single();
        reposta.FaseCodigo.Should().Be("RESULTADO_FINAL");

        // O descarte tem de devolver a etapa à fase do envelope, e não deixá-la na que a sessão
        // criou. Com uma representação só da relação isso é consequência de repor o código —
        // não há segundo apontamento que pudesse sobreviver ao descarte apontando para a fase
        // errada, que é justamente a inconsistência que a remoção da coluna eliminou.
        processo.EtapasDaFase("RESULTADO_FINAL").Should().ContainSingle();
        processo.EtapasDaFase("AVALIACAO").Should().BeEmpty();
    }

    private static FaseCronograma FaseDeHabilitacao() => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            OrigemId: new Guid("eeee0000-0000-4000-8000-000000000003"), Codigo: "HABILITACAO", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: false, PermiteComplementacao: false,
            ColetaInscricao: false, ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(
            Ordem: 1, Inicio: new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero), FaseConcluinteCodigo: null,
            UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    /// <summary>
    /// A bicondicional fase×etapa é lida pelo VÍNCULO desde que a etapa passou a declarar em que
    /// fase acontece: qualquer fase pode subdividir-se, não só a que o cadastro marca como
    /// agrupadora. A publicação já lia assim; a restauração ainda usava a regra global, e
    /// recusava justamente o envelope que a publicação aceita — o certame com a habilitação
    /// dividida em etapas podia ser publicado e decodificado, mas o descarte da retificação
    /// ficava impossível.
    /// </summary>
    [Fact(DisplayName = "Grafo com todas as etapas vinculadas a fase que não agrupa é restaurável")]
    public void GrafoComEtapasVinculadasAFaseQueNaoAgrupa_ERestaurado()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TiposDeProcessoDeTeste.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);

        FaseCronograma habilitacao = FaseDeHabilitacao();

        GrafoConfiguracao grafo = Grafo(
            etapas: [
                EtapaProcesso.Reidratar(
                    EtapaOriginal,
                    new IdentificacaoDaEtapa(Nome: "Análise documental", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "ANALISE_DOCUMENTAL", "Análise documental", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: "HABILITACAO"),
                    new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null),
                    itens: null),
            ],
            cronogramaFases: [habilitacao]);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, grafo);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.Etapas.Single().FaseCodigo.Should().Be("HABILITACAO");
    }
}
