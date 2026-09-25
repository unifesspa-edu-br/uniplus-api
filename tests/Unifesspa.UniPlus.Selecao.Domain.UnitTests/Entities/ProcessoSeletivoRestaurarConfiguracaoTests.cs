namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// Reposição da configuração congelada: uma restauração recusada não pode alterar nada do
/// agregado (tudo ou nada), e a identidade das etapas reconciliadas segue a regra de
/// preservação de <c>Id</c> por referência de negócio (Story #859 critério de aceite sobre
/// restauração tudo-ou-nada; ADR-0110 decisão sobre identidade na reidratação).
/// </summary>
/// <remarks>
/// A propriedade central aqui é <b>tudo ou nada</b>: uma restauração que falha não pode
/// deixar o agregado meio-reposto. Se a validação fosse feita dimensão a dimensão,
/// enquanto se aplica, um grafo que falhasse na <b>última</b> checagem já teria trocado
/// etapas e distribuição — e o certame ficaria numa configuração que <b>nunca existiu</b>:
/// nem a viva, nem a congelada.
/// </remarks>
public sealed class ProcessoSeletivoRestaurarConfiguracaoTests
{
    private static readonly Guid EtapaOriginal = new("aaaa0000-0000-4000-8000-000000000001");
    private static readonly Guid EtapaCongelada = new("aaaa0000-0000-4000-8000-000000000002");

    [Fact(DisplayName = "Uma restauração que falha numa validação TARDIA não altera NADA")]
    public void RestauracaoQueFalha_NaoAlteraEstado()
    {
        // A falha é TARDIA de propósito: a checagem de classificação (INV-B4, EtapaRef de
        // eliminação) só roda depois de etapas/distribuição/desempate já terem sido
        // percorridos em ValidarGrafo. Um caso trivial (etapas vazias) passaria mesmo numa
        // implementação que aplicasse etapas e distribuição antes de chegar à classificação
        // — e não testaria nada. Desde #850, a invariante ENEM×eliminação não serve mais
        // para esta prova: ela é validada dentro de ConfiguracaoClassificacao.Criar, ANTES
        // de o grafo sequer existir — usar um EtapaRef órfão é o que sobrevive na raiz
        // (depende de _etapas, fora do alcance da configuração isolada).
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.PSIQ);
        VersaoConfiguracao versao = VersaoDo(processo);

        Estado antes = Estado.De(processo);

        GrafoConfiguracao invalido = Grafo(
            etapas: [EtapaProcesso.Reidratar(EtapaCongelada, "Prova", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, 1m, null, 1)],
            eliminacoes: [
                RegraEliminacao.Criar(
                    Regra(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, 'e'),
                    new ArgsElimNotaMinimaEtapa(Guid.NewGuid(), 4m)).Value!,
            ]);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, invalido);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.EtapaRefEliminacaoInexistente");

        Estado.De(processo).Should().BeEquivalentTo(antes,
            "a validação acontece INTEIRA antes de qualquer escrita. Se a reposição aplicasse dimensão a dimensão, " +
            "este grafo já teria trocado etapas e distribuição antes de falhar na classificação — e o certame " +
            "ficaria numa configuração que nunca existiu.");
    }

    [Fact(DisplayName = "Restaurar etapa de nota do ENEM sob classificação que não é ENEM é recusado, como na gravação")]
    public void Restaurar_EtapaNotaEnemSemClassificacaoEnem_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.PSIQ);

        Result resultado = processo.RestaurarConfiguracaoCongelada(VersaoDo(processo), Grafo(etapas: [EtapaNotaEnemCongelada(EtapaCongelada, 1)]));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemSemClassificacaoEnem");
    }

    [Fact(DisplayName = "Restaurar duas etapas de nota do ENEM é recusado, como na gravação")]
    public void Restaurar_DuasEtapasNotaEnem_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.PSIQ);

        Result resultado = processo.RestaurarConfiguracaoCongelada(
            VersaoDo(processo),
            Grafo(
                etapas: [EtapaNotaEnemCongelada(EtapaCongelada, 1), EtapaNotaEnemCongelada(EtapaOriginal, 2)],
                classificacao: ClassificacaoEnemMediaPonderada()));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemDuplicada");
    }

    [Fact(DisplayName = "Restaurar etapa de nota do ENEM com banca é recusado, como na gravação")]
    public void Restaurar_EtapaNotaEnemComBanca_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.PSIQ);
        EtapaProcesso etapa = EtapaNotaEnemCongelada(EtapaCongelada, 1);
        etapa.DefinirBancas([BancaDaEtapa.Criar(Guid.CreateVersion7(), "BANCA_EXAMINADORA")]).IsSuccess.Should().BeTrue();

        Result resultado = processo.RestaurarConfiguracaoCongelada(
            VersaoDo(processo),
            Grafo(etapas: [etapa], classificacao: ClassificacaoEnemMediaPonderada()));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemComBanca");
    }

    [Theory(DisplayName = "Restaurar etapa de nota do ENEM fora da média é recusado, antes da regra geral, como na gravação")]
    [InlineData(CaraterEtapa.Eliminatoria)]
    [InlineData(CaraterEtapa.Classificatoria)]
    [InlineData(CaraterEtapa.Ambas)]
    public void Restaurar_EtapaNotaEnemForaDaMedia_Recusa(CaraterEtapa carater)
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.PSIQ);
        EtapaProcesso etapa = EtapaProcesso.Reidratar(EtapaCongelada, "Nota do ENEM", carater, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "NOTA_ENEM", "Nota do ENEM", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: true).Value!, null, null, 1);

        Result resultado = processo.RestaurarConfiguracaoCongelada(
            VersaoDo(processo),
            Grafo(etapas: [etapa], classificacao: ClassificacaoEnemMediaPonderada()));

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemNaoCompoeNota");
    }

    [Fact(DisplayName = "Restaurar etapa de nota do ENEM com parecer e recurso por ciência é aceito, como na gravação")]
    public void Restaurar_EtapaNotaEnemComParecerERecursoPorCiencia_Aceita()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.PSIQ);
        EtapaProcesso etapa = EtapaNotaEnemCongelada(EtapaCongelada, 1);
        etapa.DefinirJanelaEParecer(null, null, emiteParecerIndividual: true).IsSuccess.Should().BeTrue();
        etapa.DefinirRecursos([RecursoDaEtapaDeTeste(AncoraDoRecurso.CienciaIndividual, Guid.Empty)]).IsSuccess.Should().BeTrue();

        Result resultado = processo.RestaurarConfiguracaoCongelada(
            VersaoDo(processo),
            Grafo(etapas: [etapa], classificacao: ClassificacaoEnemMediaPonderada()));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Restaurar etapa de nota do ENEM com produto ou recurso em ato é recusado, como na gravação")]
    public void Restaurar_EtapaNotaEnemComProdutoOuRecursoEmAto_Recusa()
    {
        EtapaProcesso comProduto = EtapaNotaEnemCongelada(EtapaCongelada, 1);
        comProduto.DefinirProdutos([ProdutoDaEtapa.Criar("RESULTADO_PRELIMINAR", PapelProdutoFase.Preliminar)]).IsSuccess.Should().BeTrue();
        EtapaProcesso comRecursoEmAto = EtapaNotaEnemCongelada(EtapaCongelada, 1);
        comRecursoEmAto.DefinirRecursos([RecursoDaEtapaDeTeste(AncoraDoRecurso.AtoPublicado, Guid.CreateVersion7())]).IsSuccess.Should().BeTrue();

        foreach (EtapaProcesso etapa in new[] { comProduto, comRecursoEmAto })
        {
            ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.PSIQ);
            Result resultado = processo.RestaurarConfiguracaoCongelada(
                VersaoDo(processo),
                Grafo(etapas: [etapa], classificacao: ClassificacaoEnemMediaPonderada()));

            resultado.Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemComProdutoOuRecursoEmAto");
        }
    }

    private static RecursoDaEtapa RecursoDaEtapaDeTeste(AncoraDoRecurso ancora, Guid produtoAncoraId) =>
        RecursoDaEtapa.Criar(
            ancora,
            ReferenciaRegra.Criar(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('a', 64)).Value!,
            new ArgsRegraPrazoRecurso(48m, UnidadePrazo.Horas, null, null, null, null),
            produtoAncoraId).Value!;

    [Fact(DisplayName = "Restaurar etapa de nota do ENEM sob classificação ENEM pela média ponderada é aceito")]
    public void Restaurar_EtapaNotaEnemSobClassificacaoEnemMediaPonderada_Aceita()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.PSIQ);

        Result resultado = processo.RestaurarConfiguracaoCongelada(
            VersaoDo(processo),
            Grafo(etapas: [EtapaNotaEnemCongelada(EtapaCongelada, 1)], classificacao: ClassificacaoEnemMediaPonderada()));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.Etapas.Should().ContainSingle().Which.DeclaraNotaDoEnem.Should().BeTrue();
    }

    [Fact(DisplayName = "Restaurar desempate por área do ENEM sob classificação sem quadro de pesos é recusado, como na gravação")]
    public void Restaurar_DesempatePorAreaSemQuadro_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.PSIQ);
        Estado antes = Estado.De(processo);

        Result resultado = processo.RestaurarConfiguracaoCongelada(
            VersaoDo(processo),
            Grafo(criterios: [DesempatePorArea("REDACAO")]));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.DesempateAreaEnemSemQuadro");
        Estado.De(processo).Should().BeEquivalentTo(antes);
    }

    [Fact(DisplayName = "Restaurar desempate por área que cita área fora do quadro restaurado é recusado, como na gravação")]
    public void Restaurar_DesempatePorAreaForaDoQuadro_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.PSIQ);

        Result resultado = processo.RestaurarConfiguracaoCongelada(
            VersaoDo(processo),
            Grafo(criterios: [DesempatePorArea("REDACAO", "FISICA")], classificacao: ClassificacaoEnemMediaPonderada()));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.DesempateAreaEnemForaDoQuadro");
    }

    [Fact(DisplayName = "Restaurar desempate por área com áreas do quadro restaurado é aceito")]
    public void Restaurar_DesempatePorAreaDoQuadro_Aceita()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.PSIQ);

        Result resultado = processo.RestaurarConfiguracaoCongelada(
            VersaoDo(processo),
            Grafo(criterios: [DesempatePorArea("REDACAO", "MATEMATICA")], classificacao: ClassificacaoEnemMediaPonderada()));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.CriteriosDesempate.Should().ContainSingle()
            .Which.Args.Should().BeOfType<ArgsDesempateMaiorNotaAreaEnem>()
            .Which.Areas.Should().Equal("REDACAO", "MATEMATICA");
    }

    [Fact(DisplayName = "Restaurar dois critérios de desempate que citam a mesma área é recusado, como na gravação")]
    public void Restaurar_AreaCitadaPorDoisCriterios_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.PSIQ);
        Estado antes = Estado.De(processo);

        Result resultado = processo.RestaurarConfiguracaoCongelada(
            VersaoDo(processo),
            Grafo(
                criterios: [DesempatePorArea("REDACAO"), DesempatePorArea(2, "MATEMATICA", "REDACAO")],
                classificacao: ClassificacaoEnemMediaPonderada()));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.AreaEnemCitadaPorOutroCriterio");
        Estado.De(processo).Should().BeEquivalentTo(antes);
    }

    [Fact(DisplayName = "Restaurar mais critérios de desempate que o teto é recusado, como na gravação")]
    public void Restaurar_CriteriosAcimaDoTeto_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.PSIQ);
        Estado antes = Estado.De(processo);
        CriterioDesempate[] criterios = [.. Enumerable.Range(1, ProcessoSeletivo.CriteriosDesempateMaximo + 1).Select(static ordem =>
            CriterioDesempate.Criar(ordem, Regra(CriterioDesempateCodigo.MaiorIdade, 'f'), new ArgsDesempateMaiorIdade()).Value!)];

        Result resultado = processo.RestaurarConfiguracaoCongelada(VersaoDo(processo), Grafo(criterios: criterios));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.CriteriosDesempateEmExcesso");
        Estado.De(processo).Should().BeEquivalentTo(antes);
    }

    private static CriterioDesempate DesempatePorArea(params string[] areas) => DesempatePorArea(1, areas);

    private static CriterioDesempate DesempatePorArea(int ordem, params string[] areas) =>
        CriterioDesempate.Criar(
            ordem, Regra(CriterioDesempateCodigo.MaiorNotaAreaEnem, 'e'), new ArgsDesempateMaiorNotaAreaEnem(areas)).Value!;

    [Fact(DisplayName = "RestaurarConfiguracaoCongelada produz o mesmo resultado em processos de Tipo diferente com a mesma configuração (indistinguibilidade, #850)")]
    public void RestaurarConfiguracaoCongelada_TiposDiferentesMesmaConfiguracao_ResultadoIdentico()
    {
        ProcessoSeletivo psiq = ProcessoPublicado(TipoProcesso.PSIQ);
        ProcessoSeletivo sisu = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versaoPsiq = VersaoDo(psiq);
        VersaoConfiguracao versaoSisu = VersaoDo(sisu);

        RegraEliminacao EliminacaoEnem() => RegraEliminacao.Criar(
            Regra(RegraEliminacaoCodigo.ElimCorteEmArea, 'a'), new ArgsElimCorteEmArea("REDACAO", 400m)).Value!;

        ConfiguracaoClassificacao ClassificacaoEnemValida() => ConfiguracaoClassificacao.Criar(
            regraCalculo: Regra(RegraCalculoCodigo.FormulaMediaPonderada, 'b'),
            regraArredondamento: Regra(RegraArredondamentoCodigo.PrecisaoTruncar, 'c'),
            casasArredondamento: 2,
            regraOrdemAlocacao: Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'd'),
            nOpcoesAlocacao: 1,
            regrasEliminacao: [EliminacaoEnem()],
            baseadoEmEnem: true,
            resolucaoPesoAreaEnem: QuadroPesoAreaEnemDeTeste.Resolucao,
            quadroPesoAreaEnem: QuadroPesoAreaEnemDeTeste.Completo()).Value!;

        GrafoConfiguracao GrafoComClassificacaoEnem() => new(
            etapas: [EtapaProcesso.Reidratar(EtapaCongelada, "Prova", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, 1m, null, 1)],
            ofertaAtendimento: OfertaAtendimentoEspecializado.Criar([], [], []).Value!,
            distribuicaoVagas: [Distribuicao()],
            bonusRegional: null,
            criteriosDesempate: [],
            classificacao: ClassificacaoEnemValida(),
            cronogramaFases: [FaseConforme()],
            documentosExigidos: [],
            nosExigencia: [],
            referenciaTemporalFatos: null);

        Result resultadoPsiq = psiq.RestaurarConfiguracaoCongelada(versaoPsiq, GrafoComClassificacaoEnem());
        Result resultadoSisu = sisu.RestaurarConfiguracaoCongelada(versaoSisu, GrafoComClassificacaoEnem());

        resultadoPsiq.IsSuccess.Should().BeTrue(resultadoPsiq.Error?.Message);
        resultadoSisu.IsSuccess.Should().Be(resultadoPsiq.IsSuccess,
            "o rótulo TipoProcesso não pode decidir o resultado da restauração — só BaseadoEmEnem, dado igual nos dois processos");
    }

    [Fact(DisplayName = "EtapaRef órfão também falha sem tocar no estado")]
    public void EtapaRefOrfao_NaoAlteraEstado()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);
        Estado antes = Estado.De(processo);

        GrafoConfiguracao invalido = Grafo(
            etapas: [EtapaProcesso.Reidratar(EtapaCongelada, "Prova", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, 1m, null, 1)],
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
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);

        GrafoConfiguracao invalido = Grafo(etapas: [
            EtapaProcesso.Reidratar(EtapaCongelada, "Prova A", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, 1m, null, 1),
            EtapaProcesso.Reidratar(EtapaCongelada, "Prova B", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, 2m, null, 2),
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
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        ProcessoSeletivo alheio = ProcessoPublicado(TipoProcesso.SiSU);

        Result resultado = processo.RestaurarConfiguracaoCongelada(VersaoDo(alheio), Grafo());

        resultado.IsFailure.Should().BeTrue(
            "repor num certame a configuração congelada de outro sobrescreveria o primeiro com uma configuração que " +
            "nunca foi dele — e a próxima publicação congelaria a troca");
        resultado.Error!.Code.Should().Be("VersaoConfiguracao.VersaoDeOutroProcesso");
    }

    [Fact(DisplayName = "Um processo em rascunho não tem configuração congelada a restaurar")]
    public void ProcessoEmRascunho_Recusa()
    {
        ProcessoSeletivo rascunho = ProcessoConforme(TipoProcesso.SiSU);
        ProcessoSeletivo publicado = ProcessoPublicado(TipoProcesso.SiSU);

        Result resultado = rascunho.RestaurarConfiguracaoCongelada(VersaoDo(publicado), Grafo());

        resultado.IsFailure.Should().BeTrue(
            "a reposição não é edição — ela devolve a configuração ao que a versão congelada já dizia. Num processo " +
            "que nunca publicou não há versão nenhuma, e a operação não tem sentido.");
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.RestauracaoForaDePublicado");
    }

    [Fact(DisplayName = "A etapa que sobrevive é RECONCILIADA na mesma instância (o CreatedAt não se perde)")]
    public void EtapaSobrevivente_EReconciliada()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);

        EtapaProcesso instanciaViva = processo.Etapas.Single();

        // A etapa congelada tem o MESMO Id da viva, mas dados diferentes.
        GrafoConfiguracao grafo = Grafo(etapas: [
            EtapaProcesso.Reidratar(EtapaOriginal, "Nome Restaurado", CaraterEtapa.Ambas, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, 7m, 20m, 3),
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
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);

        GrafoConfiguracao grafo = Grafo(etapas: [
            EtapaProcesso.Reidratar(
                EtapaOriginal, "Prova", CaraterEtapa.Classificatoria,
                TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!,
                1m, null, 1, "RESULTADO_FINAL"),
        ]);

        processo.RestaurarConfiguracaoCongelada(versao, grafo).IsSuccess.Should().BeTrue();

        processo.Etapas.Single().FaseCodigo.Should().Be("RESULTADO_FINAL");
    }

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
        FaseCronograma avaliacao = FaseCronograma.Criar(
            ordem: 2,
            faseCanonicaOrigemId: new Guid("eeee0000-0000-4000-8000-000000000002"),
            codigo: "AVALIACAO",
            donoInstitucional: "CEPS",
            origemData: OrigemDataFase.Propria,
            agrupaEtapas: true,
            permiteComplementacao: false,
            coletaInscricao: false, coletaSolicitacaoIsencao: false,
            inicio: new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 2, 28, 0, 0, 0, TimeSpan.Zero),
            produtos: [],
            faseConcluinteCodigo: null,
            emiteParecerIndividual: false,
            bancasRequeridas: [],
            regraRecurso: null).Value!;

        // O estado de que o descarte parte chega por uma restauração: o agregado publicado
        // recusa mudar de fase uma etapa fora de sessão editorial, e o que se testa aqui é o
        // vínculo que a reposição refaz, não o caminho que sujou a configuração.
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);

        GrafoConfiguracao daSessao = Grafo(
            etapas: [
                EtapaProcesso.Reidratar(
                    EtapaOriginal, "Prova", CaraterEtapa.Classificatoria,
                    TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!,
                    1m, null, 1, "AVALIACAO"),
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
                EtapaProcesso.Reidratar(
                    EtapaOriginal, "Prova", CaraterEtapa.Classificatoria,
                    TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!,
                    1m, null, 1, "RESULTADO_FINAL"),
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
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);

        FaseCronograma habilitacao = FaseCronograma.Criar(
            ordem: 1,
            faseCanonicaOrigemId: new Guid("eeee0000-0000-4000-8000-000000000003"),
            codigo: "HABILITACAO",
            donoInstitucional: "CEPS",
            origemData: OrigemDataFase.Propria,
            agrupaEtapas: false,
            permiteComplementacao: false,
            coletaInscricao: false, coletaSolicitacaoIsencao: false,
            inicio: new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero),
            produtos: [],
            faseConcluinteCodigo: null,
            emiteParecerIndividual: false,
            bancasRequeridas: [],
            regraRecurso: null).Value!;

        GrafoConfiguracao grafo = Grafo(
            etapas: [
                EtapaProcesso.Reidratar(
                    EtapaOriginal, "Análise documental", CaraterEtapa.Classificatoria,
                    TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "ANALISE_DOCUMENTAL", "Análise documental", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!,
                    1m, null, 1, "HABILITACAO"),
            ],
            cronogramaFases: [habilitacao]);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, grafo);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.Etapas.Single().FaseCodigo.Should().Be("HABILITACAO");
    }

    /// <summary>
    /// A fase publica a MESMA matéria duas vezes — preliminar e definitiva — e o recurso corre
    /// da preliminar. Quando a restauração recria os produtos (a fase congelada caiu sobre uma
    /// fase viva de outra identidade), a âncora precisa reencontrar o produto pelo par ato e
    /// papel: casar só pelo ato remapeava o recurso para a publicação definitiva sempre que ela
    /// viesse primeiro, e o prazo passaria a correr do ato errado sem nada denunciar.
    /// </summary>
    [Fact(DisplayName = "A âncora restaurada reencontra o produto preliminar, e não o definitivo do mesmo ato")]
    public void AncoraRestaurada_ReencontraOPreliminarDoMesmoAto()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);

        ProdutoDaFase preliminar = ProdutoDaFase.Criar("RESULTADO", PapelProdutoFase.Preliminar);
        ProdutoDaFase definitivo = ProdutoDaFase.Criar("RESULTADO", PapelProdutoFase.Definitivo);

        FaseCronograma congelada = FaseCronograma.Criar(
            ordem: 1,
            faseCanonicaOrigemId: new Guid("eeee0000-0000-4000-8000-000000000009"),
            codigo: "RESULTADO_FINAL",
            donoInstitucional: "CEPS",
            origemData: OrigemDataFase.Propria,
            agrupaEtapas: true,
            permiteComplementacao: false,
            coletaInscricao: false, coletaSolicitacaoIsencao: false,
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            // O definitivo PRIMEIRO: é a ordem em que o envelope canônico os devolve, e é ela
            // que o casamento só por ato lia como se fosse a âncora.
            produtos: [definitivo, preliminar],
            faseConcluinteCodigo: null,
            emiteParecerIndividual: false,
            bancasRequeridas: [],
            regraRecurso: RegraRecursoFase.Criar(
                ReferenciaRegra.Criar(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('a', 64)).Value!,
                new ArgsRegraPrazoRecurso(3m, UnidadePrazo.DiasUteis, null, null, null, null),
                preliminar.Id).Value!).Value!;

        GrafoConfiguracao grafo = Grafo(cronogramaFases: [congelada]);

        processo.RestaurarConfiguracaoCongelada(versao, grafo).IsSuccess.Should().BeTrue();

        FaseCronograma reposta = processo.CronogramaFases.Single();
        ProdutoDaFase ancorado = reposta.Produtos.Single(p => p.Id == reposta.RegraRecurso!.ProdutoAncoraId);

        ancorado.Papel.Should().Be(PapelProdutoFase.Preliminar,
            "o recurso corre da publicação preliminar — é dela que o candidato toma ciência para recorrer");
    }

    /// <summary>
    /// Repor não é declarar: o grafo vem de um envelope que foi válido quando nasceu, e uma regra
    /// de coerência criada depois não pode tornar a reposição impossível — isso trancaria o
    /// certame num estado do qual o descarte da retificação nunca sairia.
    /// </summary>
    [Fact(DisplayName = "Etapa congelada com combinação que a regra atual recusaria ainda assim é reposta")]
    public void EtapaCongeladaComCombinacaoHojeRecusada_AindaERestaurada()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);

        // Nota mínima em etapa apenas classificatória: hoje o agregado recusa a declaração, e o
        // envelope antigo a carrega.
        GrafoConfiguracao grafo = Grafo(etapas: [
            EtapaProcesso.Reidratar(
                EtapaOriginal, "Prova", CaraterEtapa.Classificatoria,
                TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!,
                1m, 20m, 1),
        ]);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, grafo);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.Etapas.Single().NotaMinima.Should().Be(20m);
    }

    [Fact(DisplayName = "issue #848/ADR-0115 §3.7 — restauração com AcaoQuandoIndeferido divergente entre ofertas é recusada")]
    public void RestauracaoComAcaoQuandoIndeferidoDivergenteEntreOfertas_Recusa()
    {
        // AplicarGrafo reconstrói _distribuicaoVagas diretamente do grafo decodificado,
        // sem passar por DefinirDistribuicaoVagas — a checagem de consistência entre
        // ofertas precisa estar também em ValidarGrafo, senão a restauração de um
        // envelope congelado (que nunca poderia ter sido produzido pelo caminho normal
        // de escrita) reintroduziria o mesmo código de modalidade com ações divergentes.
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);
        Estado antes = Estado.De(processo);

        static ModalidadeSelecionada Ac(int quantidade) => ModalidadeSelecionada.Criar(
            new Guid("cccc0000-0000-4000-8000-000000000001"), "AC", null,
            NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo, null,
            RegraRemanejamentoModalidade.Nenhuma, null, null, null,
            [], null, "base legal", quantidadeDeclarada: quantidade).Value!;

        static ModalidadeSelecionada V(string acaoQuandoIndeferido, int quantidade) => ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "V", null, NaturezaLegalModalidade.Suplementar, ComposicaoVagasModalidade.SuplementarAoTotal,
            null, RegraRemanejamentoModalidade.DestinoUnico, "AC", null, null, [], acaoQuandoIndeferido, "base legal",
            quantidadeDeclarada: quantidade).Value!;

        ReferenciaRegra regra = Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a');

        ConfiguracaoDistribuicaoVagas ofertaA = ConfiguracaoDistribuicaoVagas.Criar(
            new Guid("bbbb0000-0000-4000-8000-000000000001"), voBase: 10, pr: 1m, regra,
            regraAjuste: null, referenciaDemografica: null, [V("RECLASSIFICAR_AC", 2), Ac(8)]).Value!;
        ConfiguracaoDistribuicaoVagas ofertaB = ConfiguracaoDistribuicaoVagas.Criar(
            new Guid("bbbb0000-0000-4000-8000-000000000002"), voBase: 10, pr: 1m, regra,
            regraAjuste: null, referenciaDemografica: null, [V("RECLASSIFICAR_REGRA_EDITAL", 2), Ac(8)]).Value!;

        GrafoConfiguracao invalido = new(
            etapas: [EtapaProcesso.Reidratar(EtapaCongelada, "Prova", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, 1m, null, 1)],
            ofertaAtendimento: OfertaAtendimentoEspecializado.Criar([], [], []).Value!,
            distribuicaoVagas: [ofertaA, ofertaB],
            bonusRegional: null,
            criteriosDesempate: [],
            classificacao: Classificacao([]),
            cronogramaFases: [FaseConforme()],
            documentosExigidos: [],
            nosExigencia: [],
            referenciaTemporalFatos: null);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, invalido);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.AcaoQuandoIndeferidoDivergente");
        Estado.De(processo).Should().BeEquivalentTo(antes,
            "a restauração recusada não pode deixar o agregado meio-reposto (CA-07)");
    }

    // ── NoExigencia.Reidratar não revalida os invariantes de NoExigencia.CriarGrupo — um
    // envelope 1.4 adulterado com uma folha carregando filhos, um grupo vazio ou um OU
    // pedindo mais filhos do que tem precisa ser recusado na restauração, não só no
    // SaveChanges (CHECK/índice) ou em silêncio. ──

    [Fact(DisplayName = "Árvore restaurada com uma FOLHA carregando filhos é recusada")]
    public void ArvoreComFolhaCarregandoFilhos_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);
        Estado antes = Estado.De(processo);

        FaseCronograma fase = FaseConforme();
        DocumentoExigido documento = DocumentoQualquer(fase.Id);
        NoExigencia filhoOrfao = NoExigencia.Reidratar(
            Guid.CreateVersion7(), TipoNo.Folha, 0, Guid.CreateVersion7(), DocumentoQualquer(fase.Id),
            1, null, null, null, null, null, [], []);
        NoExigencia folhaComFilhos = NoExigencia.Reidratar(
            Guid.CreateVersion7(), TipoNo.Folha, 0, documento.Id, documento,
            1, null, null, null, null, null, [], [filhoOrfao]);

        GrafoConfiguracao invalido = GrafoComArvore(fase, [documento], [folhaComFilhos, filhoOrfao]);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, invalido);

        resultado.IsFailure.Should().BeTrue(
            "NoExigencia.Reidratar não revalida os invariantes de CriarFolha/CriarGrupo — sem esta checagem, uma " +
            "folha carregando filhos só falharia depois, ao gerar consequência/resolver a árvore, e de forma não " +
            "nomeada");
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NoExigenciaFolhaComFilhos");
        Estado.De(processo).Should().BeEquivalentTo(antes, "a restauração recusada não pode deixar o agregado meio-reposto (CA-07)");
    }

    [Fact(DisplayName = "Árvore restaurada com um GRUPO vazio é recusada")]
    public void ArvoreComGrupoVazio_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);
        Estado antes = Estado.De(processo);

        FaseCronograma fase = FaseConforme();
        NoExigencia grupoVazio = NoExigencia.Reidratar(
            Guid.CreateVersion7(), TipoNo.GrupoE, 0, null, null, null, null, null, null, null, null, [], []);

        GrafoConfiguracao invalido = GrafoComArvore(fase, [], [grupoVazio]);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, invalido);

        resultado.IsFailure.Should().BeTrue(
            "um grupo E/OU sem filhos nunca é produzido por NoExigencia.CriarGrupo — só alcançável por adulteração " +
            "do envelope, e a restauração precisa recusar, não repor um grupo vazio em silêncio");
        resultado.Error!.Code.Should().Be("NoExigencia.GrupoVazio");
        Estado.De(processo).Should().BeEquivalentTo(antes);
    }

    [Fact(DisplayName = "Árvore restaurada com grupo OU pedindo mais filhos do que tem é recusada")]
    public void ArvoreComGrupoOuQuantidadeMinimaExcedeFilhos_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);
        Estado antes = Estado.De(processo);

        FaseCronograma fase = FaseConforme();
        DocumentoExigido documento = DocumentoQualquer(fase.Id);
        NoExigencia folha = NoExigencia.Reidratar(
            Guid.CreateVersion7(), TipoNo.Folha, 0, documento.Id, documento, 1, null, null, null, null, null, [], []);
        // Um único filho, mas quantidadeMinima=2 — NoExigencia.CriarGrupo nunca produziria isto
        // (o teto é filhos.Count).
        NoExigencia grupoOu = NoExigencia.Reidratar(
            Guid.CreateVersion7(), TipoNo.GrupoOu, 0, null, null, 2, "ELIMINA", null, null, null, null, [], [folha]);

        GrafoConfiguracao invalido = GrafoComArvore(fase, [documento], [grupoOu, folha]);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, invalido);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.QuantidadeMinimaForaDosLimites");
        Estado.De(processo).Should().BeEquivalentTo(antes);
    }

    [Fact(DisplayName = "Árvore restaurada com grupo OU de consequência fora do catálogo fechado é recusada")]
    public void ArvoreComGrupoOuConsequenciaInvalida_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);
        Estado antes = Estado.De(processo);

        FaseCronograma fase = FaseConforme();
        DocumentoExigido documento = DocumentoQualquer(fase.Id);
        NoExigencia folha = NoExigencia.Reidratar(
            Guid.CreateVersion7(), TipoNo.Folha, 0, documento.Id, documento, 1, null, null, null, null, null, [], []);
        // NoExigencia.CriarGrupo nunca produziria "FOO" — o catálogo fechado é
        // {ELIMINA, RECLASSIFICA_AC, REMOVE_VANTAGEM, PENDENCIA_REENVIO}.
        NoExigencia grupoOu = NoExigencia.Reidratar(
            Guid.CreateVersion7(), TipoNo.GrupoOu, 0, null, null, 1, "FOO", null, null, null, null, [], [folha]);

        GrafoConfiguracao invalido = GrafoComArvore(fase, [documento], [grupoOu, folha]);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, invalido);

        resultado.IsFailure.Should().BeTrue(
            "NoExigencia.Reidratar não revalida o vocabulário fechado de Consequencia que CriarGrupo garante — sem " +
            "esta checagem, o agregado reidrataria (e recanonicalizaria nos MESMOS bytes inválidos, provando o " +
            "round-trip) e passaria a emitir uma ConsequenciaEmitida desconhecida do vocabulário");
        resultado.Error!.Code.Should().Be("NoExigencia.ConsequenciaInvalida");
        Estado.De(processo).Should().BeEquivalentTo(antes);
    }

    [Fact(DisplayName = "Árvore restaurada com base legal de grupo sem consequência é recusada")]
    public void ArvoreComBaseLegalDeGrupoSemConsequencia_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);
        Estado antes = Estado.De(processo);

        FaseCronograma fase = FaseConforme();
        DocumentoExigido documento = DocumentoQualquer(fase.Id);
        NoExigencia folha = NoExigencia.Reidratar(
            Guid.CreateVersion7(), TipoNo.Folha, 0, documento.Id, documento, 1, null, null, null, null, null, [], []);
        NoExigenciaBaseLegal baseLegal = NoExigenciaBaseLegal.Criar(
            "Lei 12.711/2012", TipoAbrangencia.Federal, StatusBaseLegal.Resolvido, null).Value!;
        // Consequencia null + base legal presente — NoExigencia.CriarGrupo recusa isto
        // (BaseLegalSemConsequencia); Reidratar não revalida.
        NoExigencia grupoOu = NoExigencia.Reidratar(
            Guid.CreateVersion7(), TipoNo.GrupoOu, 0, null, null, 1, null, null, null, null, null, [baseLegal], [folha]);

        GrafoConfiguracao invalido = GrafoComArvore(fase, [documento], [grupoOu, folha]);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, invalido);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.BaseLegalSemConsequencia");
        Estado.De(processo).Should().BeEquivalentTo(antes);
    }

    // ── Mais três invariantes de CriarFolha/CriarGrupo que Reidratar não revalida, fechados
    // pela reconstrução via ReconstruirNoParaValidarInvariantes. ──

    [Fact(DisplayName = "Árvore restaurada com folha de cardinalidade qualificada incoerente é recusada")]
    public void ArvoreComFolhaCardinalidadeIncoerente_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);
        Estado antes = Estado.De(processo);

        FaseCronograma fase = FaseConforme();
        DocumentoExigido documento = DocumentoQualquer(fase.Id);
        // chaveDistincao COMPETENCIA_MENSAL exige dataReferencia — CriarFolha recusa isto
        // (DataReferenciaObrigatoriaParaChaveCalendario); Reidratar não revalida.
        NoExigencia folhaIncoerente = NoExigencia.Reidratar(
            Guid.CreateVersion7(), TipoNo.Folha, 0, documento.Id, documento, 1, null,
            ChaveDistincao.CompetenciaMensal, null, null, null, [], []);

        GrafoConfiguracao invalido = GrafoComArvore(fase, [documento], [folhaIncoerente]);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, invalido);

        resultado.IsFailure.Should().BeTrue(
            "NoExigencia.Reidratar não revalida a coerência chaveDistincao×dataReferencia que CriarFolha garante " +
            "— sem a reconstrução, a folha reidrataria e recanonicalizaria nos MESMOS bytes inválidos, provando o " +
            "round-trip, e SlotsEsperados() desreferenciaria DataReferencia nula em runtime");
        resultado.Error!.Code.Should().Be("NoExigencia.DataReferenciaObrigatoriaParaChaveCalendario");
        Estado.De(processo).Should().BeEquivalentTo(antes);
    }

    [Fact(DisplayName = "Árvore restaurada com repetição por entidade aninhada é recusada")]
    public void ArvoreComRepeticaoPorEntidadeAninhada_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);
        Estado antes = Estado.De(processo);

        FaseCronograma fase = FaseConforme();
        DocumentoExigido documento = DocumentoQualquer(fase.Id);
        // Folha E grupo pai ambos repetePorEntidade — CriarGrupo recusa isto
        // (RepeticaoDeEntidadeAninhada); Reidratar não revalida.
        NoExigencia folhaRepetida = NoExigencia.Reidratar(
            Guid.CreateVersion7(), TipoNo.Folha, 0, documento.Id, documento, 1, null, null, null, null,
            TipoEntidade.MembroNucleoFamiliar, [], []);
        NoExigencia grupoRepetido = NoExigencia.Reidratar(
            Guid.CreateVersion7(), TipoNo.GrupoE, 0, null, null, null, null, null, null, null,
            TipoEntidade.PessoaJuridicaVinculada, [], [folhaRepetida]);

        GrafoConfiguracao invalido = GrafoComArvore(fase, [documento], [grupoRepetido, folhaRepetida]);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, invalido);

        resultado.IsFailure.Should().BeTrue(
            "repetição não aninha (Story #922) — CriarGrupo recusa um nó repetido com descendente também repetido, " +
            "e Reidratar não revalida isso");
        resultado.Error!.Code.Should().Be("NoExigencia.RepeticaoDeEntidadeAninhada");
        Estado.De(processo).Should().BeEquivalentTo(antes);
    }

    [Fact(DisplayName = "Árvore restaurada com duas folhas para o mesmo DocumentoExigido é recusada")]
    public void ArvoreComDocumentoExigidoDuplicado_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);
        Estado antes = Estado.De(processo);

        FaseCronograma fase = FaseConforme();
        DocumentoExigido documento = DocumentoQualquer(fase.Id);
        // Duas raízes-folha distintas apontando para o MESMO DocumentoExigido — a checagem
        // de "folha referencia um documento que existe" (HashSet) não pega isto sozinha;
        // ux_nos_exigencia_documento_exigido_id só falharia depois, no SaveChanges.
        NoExigencia folhaA = NoExigencia.Reidratar(
            Guid.CreateVersion7(), TipoNo.Folha, 0, documento.Id, documento, 1, null, null, null, null, null, [], []);
        NoExigencia folhaB = NoExigencia.Reidratar(
            Guid.CreateVersion7(), TipoNo.Folha, 1, documento.Id, documento, 1, null, null, null, null, null, [], []);

        GrafoConfiguracao invalido = GrafoComArvore(fase, [documento], [folhaA, folhaB]);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, invalido);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NoExigenciaDocumentoExigidoDuplicado");
        Estado.De(processo).Should().BeEquivalentTo(antes);
    }

    [Fact(DisplayName = "Árvore restaurada com folha de quantidadeMinima nula é recusada — CriarFolha a normalizaria em silêncio")]
    public void ArvoreComFolhaQuantidadeMinimaNula_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);
        Estado antes = Estado.De(processo);

        FaseCronograma fase = FaseConforme();
        DocumentoExigido documento = DocumentoQualquer(fase.Id);
        // quantidadeMinima: null — CriarFolha normalizaria para QuantidadeMinimaPadrao (1) em
        // vez de recusar, e a RECONSTRUÇÃO sozinha sucederia; mas o nó DECODIFICADO (aplicado
        // ao agregado) continuaria com null, violando ck_nos_exigencia_tipo_campos_coerentes
        // no SaveChanges. ValidarCanonicidade compara reconstruído × decodificado e recusa
        // antes disso.
        NoExigencia folhaSemQuantidade = NoExigencia.Reidratar(
            Guid.CreateVersion7(), TipoNo.Folha, 0, documento.Id, documento, null, null, null, null, null, null, [], []);

        GrafoConfiguracao invalido = GrafoComArvore(fase, [documento], [folhaSemQuantidade]);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, invalido);

        resultado.IsFailure.Should().BeTrue(
            "CriarFolha normaliza quantidadeMinima nula para o padrão (1) em vez de recusar — a reconstrução " +
            "sozinha não pega isso; é a comparação contra o nó decodificado que fecha a lacuna");
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NoExigenciaQuantidadeMinimaAusente");
        Estado.De(processo).Should().BeEquivalentTo(antes);
    }

    [Fact(DisplayName = "Árvore restaurada com grupo carregando chaveDistincao (campo exclusivo de folha) é recusada")]
    public void ArvoreComGrupoCarregandoChaveDistincao_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);
        Estado antes = Estado.De(processo);

        FaseCronograma fase = FaseConforme();
        DocumentoExigido documento = DocumentoQualquer(fase.Id);
        NoExigencia folha = NoExigencia.Reidratar(
            Guid.CreateVersion7(), TipoNo.Folha, 0, documento.Id, documento, 1, null, null, null, null, null, [], []);
        // CriarGrupo nem aceita chaveDistincao como parâmetro — a reconstrução simplesmente
        // ignoraria este campo em vez de recusar, e o nó DECODIFICADO (com chaveDistincao
        // preenchida num grupo) é que seria aplicado ao agregado.
        NoExigencia grupoComChave = NoExigencia.Reidratar(
            Guid.CreateVersion7(), TipoNo.GrupoE, 0, null, null, null, null,
            ChaveDistincao.Ocorrencia, null, null, null, [], [folha]);

        GrafoConfiguracao invalido = GrafoComArvore(fase, [documento], [grupoComChave, folha]);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, invalido);

        resultado.IsFailure.Should().BeTrue(
            "chaveDistincao/dataReferencia/ocorrenciasEsperadas são exclusivos de folha — CriarGrupo nem os aceita " +
            "como parâmetro, então a reconstrução por si só não pega um grupo decodificado que os carrega");
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NoExigenciaGrupoComCampoDeFolha");
        Estado.De(processo).Should().BeEquivalentTo(antes);
    }

    [Fact(DisplayName = "Árvore restaurada com folha carregando base legal (campo exclusivo de grupo) é recusada")]
    public void ArvoreComFolhaCarregandoBaseLegal_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);
        Estado antes = Estado.De(processo);

        FaseCronograma fase = FaseConforme();
        DocumentoExigido documento = DocumentoQualquer(fase.Id);
        NoExigenciaBaseLegal baseLegal = NoExigenciaBaseLegal.Criar(
            "Lei 12.711/2012", TipoAbrangencia.Federal, StatusBaseLegal.Resolvido, null).Value!;
        // CriarFolha nem aceita basesLegais como parâmetro — base legal própria é exclusiva
        // de grupo OU/N-de. A reconstrução simplesmente a ignoraria em vez de recusar.
        NoExigencia folhaComBaseLegal = NoExigencia.Reidratar(
            Guid.CreateVersion7(), TipoNo.Folha, 0, documento.Id, documento, 1, null, null, null, null, null,
            [baseLegal], []);

        GrafoConfiguracao invalido = GrafoComArvore(fase, [documento], [folhaComBaseLegal]);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, invalido);

        resultado.IsFailure.Should().BeTrue(
            "base legal própria é exclusiva de grupo OU/N-de — CriarFolha nem a aceita como parâmetro, então a " +
            "reconstrução por si só não pega uma folha decodificada que a carrega");
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NoExigenciaFolhaComBaseLegal");
        Estado.De(processo).Should().BeEquivalentTo(antes);
    }

    [Fact(DisplayName = "A etapa que NÃO existe mais é reinserida com o Id congelado")]
    public void EtapaAusente_EReinseridaComOIdCongelado()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);

        GrafoConfiguracao grafo = Grafo(etapas: [
            EtapaProcesso.Reidratar(EtapaCongelada, "Etapa Que Voltou", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, 1m, null, 1),
        ]);

        processo.RestaurarConfiguracaoCongelada(versao, grafo).IsSuccess.Should().BeTrue();

        processo.Etapas.Should().ContainSingle()
            .Which.Id.Should().Be(EtapaCongelada,
                "o Id congelado é preservado mesmo quando a etapa foi removida durante a sessão editorial — é ele " +
                "que o etapaRef do desempate e da eliminação referenciam");
    }

    [Fact(DisplayName = "Story #554/issue #547 — restauração limpa DocumentosExigidos configurados durante a sessão")]
    public void Restauracao_LimpaDocumentosExigidosDaSessao()
    {
        // O bloco documentosExigidos.exigencias do envelope ainda é stub (PR #895..PR #900) —
        // GrafoConfiguracao não tem como reconstruir a coleção a partir de bytes que não
        // a contêm. A guarda B-01 garante que TODA versão já congelada tem zero
        // DocumentoExigido; a restauração precisa repor esse mesmo estado vazio, mesmo
        // que a sessão editorial tenha configurado exigências.
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);
        Guid faseId = processo.CronogramaFases.Single().Id;

        processo.AbrirRetificacao("Incluir exigência documental", versao, identificadorDaVersaoBase: null, "testes", DateTimeOffset.UnixEpoch)
            .IsSuccess.Should().BeTrue();

        DocumentoExigido exigencia = DocumentoExigido.Criar(
            faseId,
            tipoDocumentoOrigemId: Guid.CreateVersion7(),
            tipoDocumentoCodigo: "IDENTIDADE",
            tipoDocumentoNome: "Documento de identidade",
            tipoDocumentoCategoria: "PESSOAL",
            aplicabilidade: Aplicabilidade.Geral,
            obrigatorio: true,
            consequenciaIndeferimento: null,
            condicoes: [], basesLegais: [], idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(exigencia, 0).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();
        processo.DocumentosExigidos.Should().ContainSingle();

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, Grafo());

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.DocumentosExigidos.Should().BeEmpty(
            "a versão congelada nunca poderia ter sido publicada com exigência configurada (B-01) — restaurar " +
            "precisa repor esse estado vazio, não preservar o que a sessão descartada editou");
    }

    [Fact(DisplayName = "Story #554/issue #892 — restauração limpa ReferenciaTemporalFatos definida durante a sessão")]
    public void Restauracao_LimpaReferenciaTemporalFatosDaSessao()
    {
        // Mesmo raciocínio de Restauracao_LimpaDocumentosExigidosDaSessao: o campo não é
        // materializado no envelope (isso é da PR #903), então não há valor congelado para
        // restaurar — e a versão congelada nunca teve gatilho por FAIXA_ETARIA que
        // dependesse dele (B-01 barra qualquer DocumentoExigido). Preservar o valor
        // editado pela sessão descartada vazaria a mutação não publicada.
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);
        Guid faseId = processo.CronogramaFases.Single().Id;

        processo.AbrirRetificacao("Ajustar referência temporal", versao, identificadorDaVersaoBase: null, "testes", DateTimeOffset.UnixEpoch)
            .IsSuccess.Should().BeTrue();

        ReferenciaTemporalFatos referencia = ReferenciaTemporalFatos.Criar(ReferenciaTipo.FimFase, null, faseId).Value!;
        processo.DefinirReferenciaTemporalFatos(referencia, PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        processo.ReferenciaTemporalFatos.Should().NotBeNull();

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, Grafo());

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.ReferenciaTemporalFatos.Should().BeNull(
            "a versão congelada não materializa este campo (PR #903) e nunca dependeu dele — restaurar precisa " +
            "repor a ausência, não preservar o que a sessão descartada editou");
    }

    // ── issue #563 — CA-13: AplicarGrafo repõe ConfiguracaoDivulgacao campo a campo (bloco
    // ampliado) ou como ausência (bloco default, D5) — nunca preserva o que a sessão editou. ──

    [Fact(DisplayName = "issue #563 (CA-13, bloco ampliado): restaurar repõe ConfiguracaoDivulgacao campo a campo, substituindo o que a sessão editorial gravou")]
    public void Restauracao_RepoeConfiguracaoDivulgacaoAmpliadaCampoACampo()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);

        // A sessão editorial grava OUTRA configuração — é o estado que a restauração tem de
        // substituir, não preservar.
        processo.AbrirRetificacao("Editar a divulgação", versao, identificadorDaVersaoBase: null, "testes", DateTimeOffset.UnixEpoch)
            .IsSuccess.Should().BeTrue();
        processo.DefinirConfiguracaoDivulgacao(
            ConfiguracaoDivulgacao.Criar(["numero_inscricao"], null).Value!, PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        ConfiguracaoDivulgacao congelada = ConfiguracaoDivulgacao.Criar(
            ["numero_inscricao", "nome"], "Justificativa da versão congelada.").Value!;

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, Grafo(configuracaoDivulgacao: congelada));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.ConfiguracaoDivulgacao.Should().NotBeNull();
        processo.ConfiguracaoDivulgacao!.CamposPublicos.Should().Equal(congelada.CamposPublicos);
        processo.ConfiguracaoDivulgacao.Justificativa.Should().Be("Justificativa da versão congelada.");
        processo.ConfiguracaoDivulgacao.ProcessoSeletivoId.Should().Be(processo.Id);
    }

    [Fact(DisplayName = "issue #563 (CA-13, bloco default/D5): restaurar repõe ausência mesmo que a sessão editorial tenha gravado uma configuração explícita")]
    public void Restauracao_RepoeAusenciaDeConfiguracaoDivulgacaoQuandoOGrafoCongeladoENulo()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);

        processo.AbrirRetificacao("Editar a divulgação", versao, identificadorDaVersaoBase: null, "testes", DateTimeOffset.UnixEpoch)
            .IsSuccess.Should().BeTrue();
        processo.DefinirConfiguracaoDivulgacao(
            ConfiguracaoDivulgacao.Criar(["numero_inscricao", "nome_abreviado"], null).Value!, PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();
        processo.ConfiguracaoDivulgacao.Should().NotBeNull("pré-condição: a sessão editorial gravou uma configuração explícita");

        // O grafo congelado não traz configuração alguma (D5: o certame nunca configurou
        // divulgação, ou configurou exatamente o default) — a restauração não fabrica entidade.
        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, Grafo(configuracaoDivulgacao: null));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.ConfiguracaoDivulgacao.Should().BeNull(
            "D5: a ausência no grafo congelado é o efetivo default — repor precisa devolver o processo a essa " +
            "ausência, não preservar a configuração explícita que a sessão descartada editou");
    }

    [Fact(DisplayName = "Story #554, PR #903 (achado de revisão P2) — restaurar remapeia ExigidoNaFaseId/ReferenciaTemporalFatos.FaseId para a fase VIVA quando a sessão editorial trocou a fase da mesma Ordem")]
    public void Restauracao_RemapeiaReferenciasDeFaseParaAInstanciaViva()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);

        processo.AbrirRetificacao("Trocar a fase da Ordem 1", versao, identificadorDaVersaoBase: null, "testes", DateTimeOffset.UnixEpoch)
            .IsSuccess.Should().BeTrue();

        // A sessão editorial troca a fase da Ordem 1 por uma fase GENUINAMENTE diferente
        // (FaseCanonicaOrigemId novo, não reaproveita a identidade estável da fase
        // publicada) — DefinirCronogramaFases reconcilia por FaseCanonicaOrigemId (CA-04),
        // então isto insere uma instância NOVA em vez de atualizar a existente no lugar.
        FaseCronograma faseTrocada = FaseCronograma.Criar(
            ordem: 1,
            faseCanonicaOrigemId: Guid.CreateVersion7(),
            codigo: "RESULTADO_FINAL",
            donoInstitucional: "CEPS",
            origemData: OrigemDataFase.Propria,
            agrupaEtapas: true,
            permiteComplementacao: false,
            coletaInscricao: false, coletaSolicitacaoIsencao: false,
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            produtos: [ProdutoDaFase.Criar("RESULTADO_FINAL", PapelProdutoFase.Definitivo)],
            faseConcluinteCodigo: null,
            emiteParecerIndividual: false,
            bancasRequeridas: [],
            regraRecurso: null).Value!;
        processo.DefinirCronogramaFases([faseTrocada], [], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        Guid faseVivaId = processo.CronogramaFases.Single().Id;

        // O grafo CONGELADO referencia a fase que existia QUANDO a versão foi publicada —
        // um Id diferente do da fase viva acima (Reidratar preserva o Id congelado no
        // envelope 1.2, ADR-0110 D2), mas na MESMA Ordem — o caso que a reconciliação por
        // Ordem de AplicarGrafo reusa a instância viva em vez da decodificada.
        Guid faseCongeladaId = Guid.CreateVersion7();
        FaseCronograma faseCongelada = FaseCronograma.Reidratar(
            faseCongeladaId, ordem: 1, faseCanonicaOrigemId: Guid.CreateVersion7(), codigo: "RESULTADO_FINAL",
            donoInstitucional: "CEPS", origemData: OrigemDataFase.Propria, agrupaEtapas: true,
            permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false,
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            produtos: [ProdutoDaFase.Criar("RESULTADO_FINAL", PapelProdutoFase.Definitivo)],
            faseConcluinteCodigo: null,
            emiteParecerIndividual: false,
            bancasRequeridas: [], regraRecurso: null);

        DocumentoExigido documentoCongelado = DocumentoExigido.Reidratar(
            Guid.CreateVersion7(), exigidoNaFaseId: faseCongeladaId, tipoDocumentoOrigemId: Guid.CreateVersion7(),
            tipoDocumentoCodigo: "IDENTIDADE", tipoDocumentoNome: "Documento de identidade",
            tipoDocumentoCategoria: "PESSOAL", aplicabilidade: Aplicabilidade.Geral, obrigatorio: true,
            consequenciaIndeferimento: null, condicoes: [], basesLegais: [],
            idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null);

        ReferenciaTemporalFatos referenciaCongelada = ReferenciaTemporalFatos.Criar(
            ReferenciaTipo.FimFase, null, faseCongeladaId).Value!;

        GrafoConfiguracao grafoCongelado = new(
            etapas: [EtapaProcesso.Reidratar(EtapaCongelada, "Prova", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, 1m, null, 1)],
            ofertaAtendimento: OfertaAtendimentoEspecializado.Criar([], [], []).Value!,
            distribuicaoVagas: [Distribuicao()],
            bonusRegional: null,
            criteriosDesempate: [],
            classificacao: Classificacao([]),
            cronogramaFases: [faseCongelada],
            documentosExigidos: [documentoCongelado],
            nosExigencia: [],
            referenciaTemporalFatos: referenciaCongelada);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, grafoCongelado);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);

        FaseCronograma faseReposta = processo.CronogramaFases.Single();
        faseReposta.Id.Should().Be(faseVivaId,
            "a reconciliação de fases é por Ordem, não por Id (ux_fases_cronograma_processo_ordem) — a instância " +
            "VIVA sobrevive, não a decodificada");

        processo.DocumentosExigidos.Single().ExigidoNaFaseId.Should().Be(faseVivaId,
            "sem o remapeamento, o documento restaurado ficaria com ExigidoNaFaseId apontando para o Id " +
            "CONGELADO — ausente de CronogramaFases após a restauração (achado de revisão da PR #903)");

        processo.ReferenciaTemporalFatos!.FaseId.Should().Be(faseVivaId,
            "mesmo raciocínio do documento exigido: FIM_FASE precisa apontar para uma fase que realmente existe " +
            "em CronogramaFases após a restauração");
    }

    // ── Fábrica de cenários ──

    private static EtapaProcesso EtapaNotaEnemCongelada(Guid id, int ordem) =>
        EtapaProcesso.Reidratar(id, "Nota do ENEM", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "NOTA_ENEM", "Nota do ENEM", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: true).Value!, 1m, null, ordem);

    private static ReferenciaRegra Regra(string codigo, char semente) =>
        ReferenciaRegra.Criar(codigo, "v1", new string(semente, 64)).Value!;

    private static ProcessoSeletivo ProcessoConforme(TipoProcessoSnapshot tipo)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Restauração", tipo, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());

        processo.DefinirEtapas([
            EtapaProcesso.Reidratar(EtapaOriginal, "Prova Original", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, 1m, null, 1),
        ], PrecondicaoIfMatch.Ausente);
        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente);
        processo.DefinirDistribuicaoVagas([Distribuicao()], PrecondicaoIfMatch.Ausente);
        processo.DefinirClassificacao(Classificacao([]), PrecondicaoIfMatch.Ausente);
        processo.DefinirCronogramaFases([FaseConforme()], [], PrecondicaoIfMatch.Ausente);

        // Issue #1112: publicar sem declarar cobrança de taxa é recusado (CA-01).
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!,
            PrecondicaoIfMatch.Ausente);

        return processo;
    }

    private static ProcessoSeletivo ProcessoPublicado(TipoProcessoSnapshot tipo)
    {
        ProcessoSeletivo processo = ProcessoConforme(tipo);

        processo.Publicar(
            Dados(),
            configuracaoCongeladaCanonica: [1, 2, 3],
            schemaVersion: "1.1",
            algoritmoHash: "canonical-json/sha256@v1",
            hashDocumento: new string('a', 64),
            atorUsuarioSub: "testes",
            clock: TimeProvider.System, ContextoDeContagemDePrazos.SemCalendario).IsSuccess.Should().BeTrue();

        processo.ClearDomainEvents();
        return processo;
    }

    /// <summary>
    /// A versão que autentica a reposição. Os bytes não importam neste nível — a prova de
    /// que o grafo veio <b>daquela</b> versão é do <c>RestauradorDeConfiguracao</c>
    /// (Application), que recanonicaliza; o Domain não canonicaliza (ADR-0042).
    /// </summary>
    private static VersaoConfiguracao VersaoDo(ProcessoSeletivo processo) => VersaoConfiguracao.Abrir(
        processo.Id,
        [1, 2, 3],
        schemaVersion: "1.1",
        algoritmoHash: "canonical-json/sha256@v1",
        atoCriadorId: Guid.CreateVersion7(),
        atoCriadorHash: new string('a', 64),
        atorUsuarioSub: "testes",
        instante: DateTimeOffset.UnixEpoch);

    private static GrafoConfiguracao Grafo(
        IReadOnlyList<EtapaProcesso>? etapas = null,
        IReadOnlyList<CriterioDesempate>? criterios = null,
        IReadOnlyList<RegraEliminacao>? eliminacoes = null,
        IReadOnlyList<FaseCronograma>? cronogramaFases = null,
        ConfiguracaoDivulgacao? configuracaoDivulgacao = null,
        ConfiguracaoClassificacao? classificacao = null) => new(
            etapas: etapas ?? [EtapaProcesso.Reidratar(EtapaCongelada, "Prova", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, 1m, null, 1)],
            ofertaAtendimento: OfertaAtendimentoEspecializado.Criar([], [], []).Value!,
            distribuicaoVagas: [Distribuicao()],
            bonusRegional: null,
            criteriosDesempate: criterios ?? [],
            classificacao: classificacao ?? Classificacao(eliminacoes ?? []),
            cronogramaFases: cronogramaFases ?? [FaseConforme()],
            documentosExigidos: [],
            nosExigencia: [],
            referenciaTemporalFatos: null,
            configuracaoDivulgacao: configuracaoDivulgacao);

    /// <summary>
    /// Mesmo grafo de <see cref="Grafo"/>, com <c>documentosExigidos</c>/<c>nosExigencia</c>
    /// explícitos — usado pelos testes de árvore de satisfação (Story #923), que precisam de
    /// uma fase congelada específica para <paramref name="documentosExigidos"/> referenciar.
    /// </summary>
    private static GrafoConfiguracao GrafoComArvore(
        FaseCronograma fase, IReadOnlyList<DocumentoExigido> documentosExigidos, IReadOnlyList<NoExigencia> nosExigencia) => new(
            etapas: [EtapaProcesso.Reidratar(EtapaCongelada, "Prova", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, 1m, null, 1)],
            ofertaAtendimento: OfertaAtendimentoEspecializado.Criar([], [], []).Value!,
            distribuicaoVagas: [Distribuicao()],
            bonusRegional: null,
            criteriosDesempate: [],
            classificacao: Classificacao([]),
            cronogramaFases: [fase],
            documentosExigidos: documentosExigidos,
            nosExigencia: nosExigencia,
            referenciaTemporalFatos: null);

    private static DocumentoExigido DocumentoQualquer(Guid faseId) => DocumentoExigido.Criar(
        faseId, Guid.CreateVersion7(), "IDENTIDADE", "Documento de identidade", "PESSOAL",
        Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null, [], [], null,
        FormatosPermitidos.Criar(true, null).Value!, null).Value!;

    /// <summary>Uma fase mínima e conforme: agrupa etapas (há 1 etapa por padrão) e produz resultado (há vagas por padrão).</summary>
    private static FaseCronograma FaseConforme() => FaseCronograma.Criar(
        ordem: 1,
        faseCanonicaOrigemId: new Guid("eeee0000-0000-4000-8000-000000000001"),
        codigo: "RESULTADO_FINAL",
        donoInstitucional: "CEPS",
        origemData: OrigemDataFase.Propria,
        agrupaEtapas: true,
        permiteComplementacao: false,
        coletaInscricao: false, coletaSolicitacaoIsencao: false,
        inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
        produtos: [ProdutoDaFase.Criar("RESULTADO_FINAL", PapelProdutoFase.Definitivo)],
        faseConcluinteCodigo: null,
        emiteParecerIndividual: false,
        bancasRequeridas: [],
        regraRecurso: null).Value!;

    private static ConfiguracaoDistribuicaoVagas Distribuicao() =>
        ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: new Guid("bbbb0000-0000-4000-8000-000000000001"),
            voBase: 40,
            pr: 1m,
            regraDistribuicao: Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'),
            regraAjuste: null,
            referenciaDemografica: null,
            modalidades: [
                ModalidadeSelecionada.Criar(
                    new Guid("cccc0000-0000-4000-8000-000000000001"), "AC", null,
                    NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo, null,
                    RegraRemanejamentoModalidade.Nenhuma, null, null, null,
                    [], null, "Res. Unifesspa 532/2021", quantidadeDeclarada: 40).Value!,
            ]).Value!;

    private static ConfiguracaoClassificacao Classificacao(IReadOnlyList<RegraEliminacao> eliminacoes) =>
        ConfiguracaoClassificacao.Criar(
            regraCalculo: Regra(RegraCalculoCodigo.FormulaMediaPonderada, 'b'),
            regraArredondamento: Regra(RegraArredondamentoCodigo.PrecisaoTruncar, 'c'),
            casasArredondamento: 2,
            regraOrdemAlocacao: Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'd'),
            nOpcoesAlocacao: 1,
            regrasEliminacao: eliminacoes,
            baseadoEmEnem: false,
            resolucaoPesoAreaEnem: null,
            quadroPesoAreaEnem: []).Value!;

    private static ConfiguracaoClassificacao ClassificacaoEnemMediaPonderada() =>
        ConfiguracaoClassificacao.Criar(
            regraCalculo: Regra(RegraCalculoCodigo.FormulaMediaPonderada, 'b'),
            regraArredondamento: Regra(RegraArredondamentoCodigo.PrecisaoTruncar, 'c'),
            casasArredondamento: 2,
            regraOrdemAlocacao: Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'd'),
            nOpcoesAlocacao: 1,
            regrasEliminacao: [],
            baseadoEmEnem: true,
            resolucaoPesoAreaEnem: QuadroPesoAreaEnemDeTeste.Resolucao,
            quadroPesoAreaEnem: QuadroPesoAreaEnemDeTeste.Completo()).Value!;

    private static DadosEdital Dados() => DadosEdital.Criar(
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
    private sealed record Estado(
        StatusProcesso Status,
        IReadOnlyList<(Guid Id, string Nome, decimal? Peso, int? Ordem)> Etapas,
        int Condicoes,
        IReadOnlyList<(Guid Oferta, int VoBase, decimal Pr, int Modalidades)> Distribuicao,
        bool TemBonus,
        IReadOnlyList<int> OrdensDesempate,
        string RegraCalculo,
        int Eliminacoes)
    {
        internal static Estado De(ProcessoSeletivo processo) => new(
            processo.Status,
            [.. processo.Etapas.Select(e => (e.Id, e.Nome, e.Peso, e.Ordem)).OrderBy(e => e.Id)],
            processo.OfertaAtendimento!.Condicoes.Count,
            [.. processo.DistribuicaoVagas
                .Select(d => (d.OfertaCursoOrigemId, d.VoBase, d.Pr, d.Modalidades.Count))
                .OrderBy(d => d.OfertaCursoOrigemId)],
            processo.BonusRegional is not null,
            [.. processo.CriteriosDesempate.Select(c => c.Ordem).Order()],
            processo.Classificacao!.RegraCalculo.Codigo,
            processo.Classificacao.RegrasEliminacao.Count);
    }
}
