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

public sealed partial class ProcessoSeletivoRestaurarConfiguracaoTests
{
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
            origemData: OrigemDataFase.NoEdital,
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
                new ArgsRegraPrazoRecurso(3m, UnidadePrazo.DiasUteis, null, null, false, null, null),
                preliminar.Id).Value!,
            unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;

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
                1m, 20m, 1, notaMaxima: 10m, formaDeAvaliacao: FormaDeAvaliacao.Objetiva),
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
            RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), null, "base legal", quantidadeDeclarada: quantidade).Value!;

        static ModalidadeSelecionada V(string acaoQuandoIndeferido, int quantidade) => ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "V", null, NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.SuplementarAoTotal,
            null, RegraRemanejamentoModalidade.DestinoUnico, "AC", null, null, RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.AcaoAfirmativa), acaoQuandoIndeferido, "base legal",
            quantidadeDeclarada: quantidade).Value!;

        ReferenciaRegra regra = Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a');

        ConfiguracaoDistribuicaoVagas ofertaA = ConfiguracaoDistribuicaoVagas.Criar(
            new Guid("bbbb0000-0000-4000-8000-000000000001"), voBase: 10, pr: 1m, regra,
            regraAjuste: null, referenciaDemografica: null, [V("RECLASSIFICAR_AC", 2), Ac(8)], ofertaCurso: OfertaCursoDeTeste.Medicina).Value!;
        ConfiguracaoDistribuicaoVagas ofertaB = ConfiguracaoDistribuicaoVagas.Criar(
            new Guid("bbbb0000-0000-4000-8000-000000000002"), voBase: 10, pr: 1m, regra,
            regraAjuste: null, referenciaDemografica: null, [V("RECLASSIFICAR_REGRA_EDITAL", 2), Ac(8)], ofertaCurso: OfertaCursoDeTeste.Medicina).Value!;

        GrafoConfiguracao invalido = new(
            etapas: [EtapaProcesso.Reidratar(EtapaCongelada, "Prova", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, 1m, null, 1, notaMaxima: 10m, formaDeAvaliacao: FormaDeAvaliacao.Objetiva)],
            ofertaAtendimento: OfertaAtendimentoEspecializado.Criar([], [], []).Value!,
            distribuicaoVagas: [ofertaA, ofertaB],
            bonusRegional: null,
            criteriosDesempate: [],
            classificacao: Classificacao([]),
            cronogramaFases: [FaseConforme()],
            documentosExigidos: [],
            nosExigencia: [],
            referenciaTemporalFatos: null,
            numeroDeOpcoesDeCurso: 1);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, invalido);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.AcaoQuandoIndeferidoDivergente");
        Estado.De(processo).Should().BeEquivalentTo(antes,
            "a restauração recusada não pode deixar o agregado meio-reposto (CA-07)");
    }

    [Fact(DisplayName = "Restauração com a regra de uma cota da lei sem a opção pelas cotas é recusada")]
    public void RestauracaoComRegraIncoerenteComANatureza_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);
        Estado antes = Estado.De(processo);

        ModalidadeSelecionada ac = ModalidadeSelecionada.Criar(
            new Guid("cccc0000-0000-4000-8000-000000000001"), "AC", null,
            NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo, null,
            RegraRemanejamentoModalidade.Nenhuma, null, null, null, PredicadoDnf.SemClausula, null, "base legal", quantidadeDeclarada: 8).Value!;
        ModalidadeSelecionada cotaSemOpcao = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "LI_EP", null, NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.SuplementarAoTotal,
            null, RegraRemanejamentoModalidade.SegueCascata, null, null, null, PredicadoDnf.SemClausula, null, "base legal",
            quantidadeDeclarada: 2).Value!;
        ConfiguracaoDistribuicaoVagas oferta = ConfiguracaoDistribuicaoVagas.Criar(
            new Guid("bbbb0000-0000-4000-8000-000000000001"), voBase: 10, pr: 1m, Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'),
            regraAjuste: null, referenciaDemografica: null, [cotaSemOpcao, ac], ofertaCurso: OfertaCursoDeTeste.Medicina).Value!;

        GrafoConfiguracao invalido = new(
            etapas: [EtapaProcesso.Reidratar(EtapaCongelada, "Prova", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, 1m, null, 1, notaMaxima: 10m, formaDeAvaliacao: FormaDeAvaliacao.Objetiva)],
            ofertaAtendimento: OfertaAtendimentoEspecializado.Criar([], [], []).Value!,
            distribuicaoVagas: [oferta],
            bonusRegional: null,
            criteriosDesempate: [],
            classificacao: Classificacao([]),
            cronogramaFases: [FaseConforme()],
            documentosExigidos: [],
            nosExigencia: [],
            referenciaTemporalFatos: null,
            numeroDeOpcoesDeCurso: 1);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, invalido);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.RegraIncoerenteComANatureza);
        Estado.De(processo).Should().BeEquivalentTo(antes, "a restauração recusada não pode deixar o agregado meio-reposto");
    }

    [Fact(DisplayName = "Restauração com uma ação afirmativa sem remanejamento é recusada")]
    public void RestauracaoComAcaoAfirmativaSemRemanejamento_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);
        Estado antes = Estado.De(processo);

        ModalidadeSelecionada ac = ModalidadeSelecionada.Criar(
            new Guid("cccc0000-0000-4000-8000-000000000001"), "AC", null,
            NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo, null,
            RegraRemanejamentoModalidade.Nenhuma, null, null, null, PredicadoDnf.SemClausula, null, "base legal", quantidadeDeclarada: 8).Value!;
        ModalidadeSelecionada reservaSemRemanejamento = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "AC_PCD", null, NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.RetiraDe,
            "AC", RegraRemanejamentoModalidade.Nenhuma, null, null, null,
            RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.AcaoAfirmativa), null, "base legal",
            quantidadeDeclarada: 2).Value!;
        ConfiguracaoDistribuicaoVagas oferta = ConfiguracaoDistribuicaoVagas.Criar(
            new Guid("bbbb0000-0000-4000-8000-000000000001"), voBase: 10, pr: 1m, Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'),
            regraAjuste: null, referenciaDemografica: null, [reservaSemRemanejamento, ac], ofertaCurso: OfertaCursoDeTeste.Medicina).Value!;

        GrafoConfiguracao invalido = new(
            etapas: [EtapaProcesso.Reidratar(EtapaCongelada, "Prova", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, 1m, null, 1, notaMaxima: 10m, formaDeAvaliacao: FormaDeAvaliacao.Objetiva)],
            ofertaAtendimento: OfertaAtendimentoEspecializado.Criar([], [], []).Value!,
            distribuicaoVagas: [oferta],
            bonusRegional: null,
            criteriosDesempate: [],
            classificacao: Classificacao([]),
            cronogramaFases: [FaseConforme()],
            documentosExigidos: [],
            nosExigencia: [],
            referenciaTemporalFatos: null,
            numeroDeOpcoesDeCurso: 1);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, invalido);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.RemanejamentoIncoerenteComANatureza);
        Estado.De(processo).Should().BeEquivalentTo(antes, "a restauração recusada não pode deixar o agregado meio-reposto");
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
}
