namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaConformidadeDoCronograma;

/// <summary>
/// Cobertura da conferência de publicação para o tipo de ato único por objeto declarado mais de uma vez no
/// cronograma (reprovação, retificação, checklist) e para o aviso do mesmo tipo definitivo em mais de um lugar.
/// </summary>
public sealed class ConformidadeDoTipoDeAtoUnicoTests
{
    private static DomainError? PendenciaDoTipoDeAtoUnico(ConfiguracaoDeclarada configuracao, TiposDeAtoUnicosPorObjeto tipos) =>
        ConferenciaDosTiposDeAto.ConferirTiposUnicosRepetidos(CronogramaDoProcesso.Do(configuracao.Cronograma.CronogramaFases), EtapasDoProcesso.Das(configuracao.Avaliacao.Etapas), tipos) is [var primeira, ..]
            ? primeira.Error
            : null;

    private const string CodigoItemTipoUnico = "cronograma_tipo_de_ato_unico_declarado_uma_vez";

    private static TiposDeAtoUnicosPorObjeto Unicos(string codigo, string nome, string? atoDaOperacao = null, bool vagaOcupada = false) =>
        atoDaOperacao is null
            ? TiposDeAtoUnicosPorObjeto.ParaChecklist(new Dictionary<string, string> { [codigo] = nome }, [])
            : TiposDeAtoUnicosPorObjeto.ParaOperacao(
                new Dictionary<string, string> { [codigo] = nome },
                vagaOcupada ? [codigo] : [],
                atoDaOperacao,
                atoDaOperacaoEhUnico: string.Equals(atoDaOperacao, codigo, StringComparison.Ordinal),
                nomeDoAtoDaOperacao: nome);

    private static Result<VersaoConfiguracao> PublicarComUnicos(ProcessoSeletivo processo, TiposDeAtoUnicosPorObjeto unicos) =>
        processo.Publicar(
            new CenarioDeCongelamento { Dados = Dados(), Documento = new DocumentoCongelado("{}"u8.ToArray(), "1.1", "canonical-json/sha256@v1", HashFixo), AtorUsuarioContaId = "teste", TiposDeAtoUnicos = unicos });

    private static FaseCronograma FaseQuePublica(int ordem, string codigo, params ProdutoDaFase[] produtos) =>
        FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            OrigensDoTipoDeFaseDeTeste.Da(origemId: Guid.CreateVersion7(), codigo: codigo, origemData: OrigemDataFase.NaExecucao, agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(Ordem: ordem, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase(Produtos: produtos, EmiteParecerIndividual: false, RegraRecurso: null),
            bancasRequeridas: []).Value!;

    private static ProcessoSeletivo ProcessoPublicavelComCronograma(params FaseCronograma[] fases)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        processo.DefinirAtendimentoEspecializado(AtendimentoEspecializado.Criar([], [], []).Value!);
        processo.DefinirDistribuicaoVagas([Distribuicao(40)], FatosDeModalidadeDeTeste.DoCatalogo);
        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = Regra(RegraDeCalculoDaNota.ClassificacaoImportada.Codigo(), 'b'),
            RegraOrdemAlocacao = Regra(RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria.Codigo(), 'c'),
        }).Value!);
        processo.DefinirCronogramaFases([.. fases, HomologacaoDasInscricoesDeTeste.FaseQueHomologaComRecurso(ordem: 99)], []).IsSuccess.Should().BeTrue();
        DeclararNaoCobra(processo);
        ContagemDePrazosDeTeste.DeclararConvencao(processo);
        return processo;
    }

    [Fact(DisplayName = "Tipo único declarado em duas fases reprova a publicação e a mensagem nomeia o tipo e as duas fases")]
    public void TipoUnico_EmDuasFases_ReprovaNomeandoOTipoEAsFases()
    {
        ProcessoSeletivo processo = ProcessoPublicavelComCronograma(
            FaseQuePublica(1, "CLASSIFICACAO", ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelDaPublicacaoPrevista.Definitivo)),
            FaseQuePublica(2, "HABILITACAO", ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelDaPublicacaoPrevista.Definitivo)));

        Result<VersaoConfiguracao> resultado = PublicarComUnicos(processo, Unicos("RESULTADO_CLASSIFICACAO", "Resultado da classificação"));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.TipoDeAtoUnicoDeclaradoMaisDeUmaVez");
        resultado.Error.Message.Should().Be(
            "O tipo de ato 'Resultado da classificação' só pode ser publicado uma vez por processo e está declarado na fase 'CLASSIFICACAO' e na fase 'HABILITACAO'.");
    }

    [Fact(DisplayName = "Tipo único declarado como preliminar e como definitivo reprova")]
    public void TipoUnico_PreliminarEDefinitivo_Reprova()
    {
        ProcessoSeletivo processo = ProcessoPublicavelComCronograma(
            FaseQuePublica(1, "HOMOLOGACAO_INSCRICOES",
                ProdutoDaFase.Criar("HOMOLOGACAO_INSCRICOES", PapelDaPublicacaoPrevista.Preliminar),
                ProdutoDaFase.Criar("HOMOLOGACAO_INSCRICOES", PapelDaPublicacaoPrevista.Definitivo)));

        DomainError? pendencia = PendenciaDoTipoDeAtoUnico(processo.Declaracao(),
            Unicos("HOMOLOGACAO_INSCRICOES", "Homologação das inscrições"));

        pendencia.Should().NotBeNull();
        pendencia!.Message.Should().Contain("Homologação das inscrições").And.Contain("na fase 'HOMOLOGACAO_INSCRICOES' (2 vezes)");
    }

    [Fact(DisplayName = "Tipo único declarado sem papel e com papel reprova")]
    public void TipoUnico_SemPapelEComPapel_Reprova()
    {
        ProcessoSeletivo processo = ProcessoPublicavelComCronograma(
            FaseQuePublica(1, "HOMOLOGACAO_INSCRICOES", ProdutoDaFase.Criar("HOMOLOGACAO_INSCRICOES", null)),
            FaseQuePublica(2, "RESULTADO", ProdutoDaFase.Criar("HOMOLOGACAO_INSCRICOES", PapelDaPublicacaoPrevista.Definitivo)));

        DomainError? pendencia = PendenciaDoTipoDeAtoUnico(processo.Declaracao(),
            Unicos("HOMOLOGACAO_INSCRICOES", "Homologação das inscrições"));

        pendencia.Should().NotBeNull();
        pendencia!.Code.Should().Be("ProcessoSeletivo.TipoDeAtoUnicoDeclaradoMaisDeUmaVez");
    }

    [Fact(DisplayName = "Tipo não único declarado como preliminar e como definitivo é aceito")]
    public void TipoNaoUnico_PreliminarEDefinitivo_Aceita()
    {
        ProcessoSeletivo processo = ProcessoPublicavelComCronograma(
            FaseQuePublica(1, "HOMOLOGACAO_INSCRICOES",
                ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelDaPublicacaoPrevista.Preliminar),
                ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelDaPublicacaoPrevista.Definitivo)));

        PendenciaDoTipoDeAtoUnico(processo.Declaracao(), Unicos("HOMOLOGACAO_INSCRICOES", "Homologação das inscrições")).Should().BeNull();
    }

    [Fact(DisplayName = "Tipo único declarado numa fase e numa etapa reprova: o produto da etapa também conta")]
    public void TipoUnico_NaFaseENaEtapa_Reprova()
    {
        ProcessoSeletivo processo = ProcessoPublicavelComCronograma(
            FaseQuePublica(1, "CLASSIFICACAO", ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelDaPublicacaoPrevista.Definitivo)));
        EtapaProcesso etapa = EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
            new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 3m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
        etapa.DefinirProdutos([ProdutoDaEtapa.Criar("RESULTADO_CLASSIFICACAO", PapelDaPublicacaoPrevista.Definitivo)]).IsSuccess.Should().BeTrue();
        processo.DefinirEtapas([etapa]).IsSuccess.Should().BeTrue();

        DomainError? pendencia = PendenciaDoTipoDeAtoUnico(processo.Declaracao(),
            Unicos("RESULTADO_CLASSIFICACAO", "Resultado da classificação"));

        pendencia.Should().NotBeNull();
        pendencia!.Message.Should().Contain("na fase 'CLASSIFICACAO' e na etapa 'Prova Objetiva' (ordem 1)");
    }

    [Fact(DisplayName = "Tipo não único declarado como preliminar e como definitivo é aceito e publica")]
    public void TipoNaoUnico_PreliminarEDefinitivo_Publica()
    {
        ProcessoSeletivo processo = ProcessoPublicavelComCronograma(
            FaseQuePublica(1, "HOMOLOGACAO_INSCRICOES",
                ProdutoDaFase.Criar("RESULTADO_HOMOLOGACAO", PapelDaPublicacaoPrevista.Preliminar),
                ProdutoDaFase.Criar("RESULTADO_HOMOLOGACAO", PapelDaPublicacaoPrevista.Definitivo)));

        Result<VersaoConfiguracao> resultado = PublicarComUnicos(processo, Unicos("RESULTADO_CLASSIFICACAO", "Resultado da classificação"));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "O checklist projeta o item do tipo único: vermelho com a duplicidade, verde sem ela")]
    public void Checklist_ProjetaOItemDoTipoUnico()
    {
        ProcessoSeletivo duplicado = ProcessoPublicavelComCronograma(
            FaseQuePublica(1, "CLASSIFICACAO", ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelDaPublicacaoPrevista.Definitivo)),
            FaseQuePublica(2, "HABILITACAO", ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelDaPublicacaoPrevista.Definitivo)));
        ProcessoSeletivo unico = ProcessoPublicavelComCronograma(
            FaseQuePublica(1, "CLASSIFICACAO", ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelDaPublicacaoPrevista.Definitivo)));
        TiposDeAtoUnicosPorObjeto unicos = Unicos("RESULTADO_CLASSIFICACAO", "Resultado da classificação");

        ChecklistDeConformidade.AvaliarConformidade(duplicado.Declaracao(), CadastrosDeTeste.Com(tiposDeAtoUnicos: unicos, motivosEscolhidos: MotivosDeDecisaoDoProcessoDeTeste.Cobrindo(duplicado)))
            .Should().Contain(item => item.Codigo == CodigoItemTipoUnico && !item.Ok);
        ChecklistDeConformidade.AvaliarConformidade(unico.Declaracao(), CadastrosDeTeste.Com(tiposDeAtoUnicos: unicos, motivosEscolhidos: MotivosDeDecisaoDoProcessoDeTeste.Cobrindo(unico)))
            .Should().Contain(item => item.Codigo == CodigoItemTipoUnico && item.Ok);
    }

    [Fact(DisplayName = "O ato da própria publicação ocupa a vaga: produto do mesmo tipo único no cronograma reprova")]
    public void TipoUnico_NoAtoDaPublicacaoENoCronograma_Reprova()
    {
        ProcessoSeletivo processo = ProcessoPublicavelComCronograma(
            FaseQuePublica(1, "ABERTURA", ProdutoDaFase.Criar("EDITAL_ABERTURA", PapelDaPublicacaoPrevista.Definitivo)));

        Result<VersaoConfiguracao> resultado = PublicarComUnicos(
            processo, Unicos("EDITAL_ABERTURA", "Edital de abertura", atoDaOperacao: "EDITAL_ABERTURA"));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.TipoDeAtoUnicoDeclaradoMaisDeUmaVez");
        resultado.Error.Message.Should().Contain("na fase 'ABERTURA' e no ato que está sendo registrado");
    }

    private static EtapaProcesso EtapaDeProvaComResultadoDefinitivo(int ordem)
    {
        EtapaProcesso etapa = EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
            new IdentificacaoDaEtapa(Nome: "Prova", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: ordem, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 3m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
        etapa.DefinirProdutos([ProdutoDaEtapa.Criar("RESULTADO_CLASSIFICACAO", PapelDaPublicacaoPrevista.Definitivo)]).IsSuccess.Should().BeTrue();
        return etapa;
    }

    [Fact(DisplayName = "Duas etapas de mesmo nome em ordens distintas são contadas cada uma, com a ordem na mensagem")]
    public void TipoUnico_EmDuasEtapasDeMesmoNome_ContaCadaDeclaracao()
    {
        ProcessoSeletivo processo = ProcessoPublicavelComCronograma(
            FaseQuePublica(1, "CLASSIFICACAO"));
        processo.DefinirEtapas([EtapaDeProvaComResultadoDefinitivo(1), EtapaDeProvaComResultadoDefinitivo(2)]).IsSuccess.Should().BeTrue();

        DomainError? pendencia = PendenciaDoTipoDeAtoUnico(processo.Declaracao(), Unicos("RESULTADO_CLASSIFICACAO", "Resultado da classificação"));

        pendencia.Should().NotBeNull();
        pendencia!.Message.Should().Contain("na etapa 'Prova' (ordem 1) e na etapa 'Prova' (ordem 2)");
    }

    [Fact(DisplayName = "Retificação: o cronograma que passa a declarar o tipo único já ocupado por ato registrado do processo reprova")]
    public void TipoUnico_JaOcupadoPorAtoRegistrado_ReprovaProdutoDoCronograma()
    {
        ProcessoSeletivo processo = ProcessoPublicavelComCronograma(
            FaseQuePublica(1, "ABERTURA", ProdutoDaFase.Criar("EDITAL_ABERTURA", PapelDaPublicacaoPrevista.Definitivo)));

        DomainError? pendencia = PendenciaDoTipoDeAtoUnico(processo.Declaracao(),
            Unicos("EDITAL_ABERTURA", "Edital de abertura", atoDaOperacao: "RETIFICACAO_EDITAL", vagaOcupada: true));

        pendencia.Should().NotBeNull();
        pendencia!.Message.Should().Contain("na fase 'ABERTURA' e no ato já registrado do processo");
    }

    [Fact(DisplayName = "Retificação com ato do mesmo tipo único da própria linhagem não conta duas vezes a mesma vaga")]
    public void TipoUnico_AtoDaOperacaoEAtoRegistradoDoMesmoTipo_ContamUmaVaga()
    {
        ProcessoSeletivo processo = ProcessoPublicavelComCronograma(
            FaseQuePublica(1, "CLASSIFICACAO", ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelDaPublicacaoPrevista.Definitivo)));

        PendenciaDoTipoDeAtoUnico(processo.Declaracao(),
            Unicos("EDITAL_ABERTURA", "Edital de abertura", atoDaOperacao: "EDITAL_ABERTURA", vagaOcupada: true))
            .Should().BeNull();
    }

    [Fact(DisplayName = "Só o catálogo vigente decide a unicidade: tipo ocupado por ato registrado que o catálogo não declara único não reprova")]
    public void TipoOcupadoQueOCatalogoNaoDeclaraUnico_NaoReprova()
    {
        ProcessoSeletivo processo = ProcessoPublicavelComCronograma(
            FaseQuePublica(1, "ABERTURA", ProdutoDaFase.Criar("EDITAL_ABERTURA", PapelDaPublicacaoPrevista.Definitivo)));
        TiposDeAtoUnicosPorObjeto ocupadoMasNaoUnico = TiposDeAtoUnicosPorObjeto.ParaOperacao(
            new Dictionary<string, string>(), ["EDITAL_ABERTURA"], "RETIFICACAO_EDITAL");

        PendenciaDoTipoDeAtoUnico(processo.Declaracao(), ocupadoMasNaoUnico).Should().BeNull();
    }

    [Fact(DisplayName = "Aviso: o mesmo tipo definitivo em duas fases gera o aviso, com o código estável e as duas fases na mensagem")]
    public void Aviso_MesmoDefinitivoEmDuasFases_GeraAviso()
    {
        ProcessoSeletivo processo = ProcessoPublicavelComCronograma(
            FaseQuePublica(1, "CLASSIFICACAO", ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelDaPublicacaoPrevista.Definitivo)),
            FaseQuePublica(2, "HABILITACAO", ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelDaPublicacaoPrevista.Definitivo)));

        AvisoConformidade aviso = ChecklistDeConformidade.AvaliarAvisos(processo.Declaracao()).Should().ContainSingle().Subject;

        aviso.Codigo.Should().Be(CodigosDeAvisoDeConformidade.CronogramaTipoDefinitivoEmMaisDeUmLugar);
        aviso.Dimensao.Should().Be(DimensaoConformidade.Cronograma);
        aviso.Mensagem.Should().Contain("'RESULTADO_CLASSIFICACAO'").And.Contain("na fase 'CLASSIFICACAO' e na fase 'HABILITACAO'");
    }

    [Fact(DisplayName = "Aviso: o mesmo tipo definitivo numa fase e numa etapa gera o aviso nomeando a etapa")]
    public void Aviso_MesmoDefinitivoNaFaseENaEtapa_GeraAviso()
    {
        ProcessoSeletivo processo = ProcessoPublicavelComCronograma(
            FaseQuePublica(1, "CLASSIFICACAO", ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelDaPublicacaoPrevista.Definitivo)));
        EtapaProcesso etapa = EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
            new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 3m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
        etapa.DefinirProdutos([ProdutoDaEtapa.Criar("RESULTADO_CLASSIFICACAO", PapelDaPublicacaoPrevista.Definitivo)]).IsSuccess.Should().BeTrue();
        processo.DefinirEtapas([etapa]).IsSuccess.Should().BeTrue();

        ChecklistDeConformidade.AvaliarAvisos(processo.Declaracao()).Should().ContainSingle().Which.Mensagem
            .Should().Contain("na fase 'CLASSIFICACAO' e na etapa 'Prova Objetiva' (ordem 1)");
    }

    [Fact(DisplayName = "Aviso: o tipo definitivo em um lugar só, ainda que com preliminar, não gera aviso")]
    public void Aviso_DefinitivoEmUmLugarSo_NaoGeraAviso()
    {
        ProcessoSeletivo processo = ProcessoPublicavelComCronograma(
            FaseQuePublica(1, "HOMOLOGACAO_INSCRICOES",
                ProdutoDaFase.Criar("RESULTADO_HOMOLOGACAO", PapelDaPublicacaoPrevista.Preliminar),
                ProdutoDaFase.Criar("RESULTADO_HOMOLOGACAO", PapelDaPublicacaoPrevista.Definitivo)));

        ChecklistDeConformidade.AvaliarAvisos(processo.Declaracao()).Should().BeEmpty();
    }

    [Fact(DisplayName = "Aviso nunca impede a publicação: o processo com o aviso publica e o checklist não tem item vermelho por causa dele")]
    public void Aviso_NaoImpedeAPublicacao()
    {
        ProcessoSeletivo processo = ProcessoPublicavelComCronograma(
            FaseQuePublica(1, "CLASSIFICACAO", ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelDaPublicacaoPrevista.Definitivo)),
            FaseQuePublica(2, "HABILITACAO", ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelDaPublicacaoPrevista.Definitivo)));

        ChecklistDeConformidade.AvaliarAvisos(processo.Declaracao()).Should().NotBeEmpty();
        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo))
            .Where(static i => i.Codigo.StartsWith("cronograma_", StringComparison.Ordinal)).Should().OnlyContain(static i => i.Ok);
        PublicarComUnicos(processo, TiposDeAtoUnicosDeTeste.Nenhum).IsSuccess.Should().BeTrue();
    }
}
