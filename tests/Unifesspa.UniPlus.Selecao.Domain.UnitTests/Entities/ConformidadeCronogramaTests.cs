namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Cobertura do gate de publicação para as pendências do cronograma que só afloram no
/// congelamento (<c>ProcessoSeletivo.PendenciaDoCronograma</c>, Story #851 §3.4/§3.5):
/// piso mínimo derivado da origem dos candidatos (issue #851, CA-11), indistinguibilidade por tipo
/// (issue #851, CA-12), vagas sem fase que produz resultado (issue #851, CA-13), a direção lazy da
/// bicondicional fase×etapa (CA-14) e o divisor da média sob fórmula local (CA-15).
/// </summary>
public sealed class ConformidadeCronogramaTests
{
    private static readonly string HashFixo = new('a', 64);

    private static ReferenciaRegra Regra(string codigo, char semente) =>
        ReferenciaRegra.Criar(codigo, "v1", new string(semente, 64)).Value!;

    private static ConfiguracaoDistribuicaoVagas Distribuicao(int voBase) =>
        ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: Guid.CreateVersion7(),
            voBase: voBase,
            pr: 1m,
            regraDistribuicao: Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'),
            regraAjuste: null,
            referenciaDemografica: null,
            modalidades: [
                ModalidadeSelecionada.Criar(
                    Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla,
                    ComposicaoVagasModalidade.ResidualDoVo, null, RegraRemanejamentoModalidade.Nenhuma,
                    null, null, null, RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), null, "Res. Unifesspa 532/2021", quantidadeDeclarada: voBase).Value!,
            ], ofertaCurso: OfertaCursoDeTeste.Medicina).Value!;

    private static Result<FaseCronograma> Fase(
        int ordem,
        string codigo,
        bool agrupaEtapas = false,
        bool produzResultado = false,
        bool coletaInscricao = false,
        bool coletaSolicitacaoIsencao = false) =>
        FaseCronograma.Criar(
            ordem, Guid.CreateVersion7(), codigo, OrigemDataFase.NaExecucao,
            agrupaEtapas, permiteComplementacao: false,
            coletaInscricao, coletaSolicitacaoIsencao,
            inicio: null, fim: null,
            produtos: produzResultado ? [ProdutoDaFase.Criar(codigo, PapelProdutoFase.Definitivo)] : [],
            faseConcluinteCodigo: null, emiteParecerIndividual: false,
            bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps);

    private static DadosEdital Dados() => DadosEdital.Criar(
        "001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)), new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)), Guid.CreateVersion7()).Value!;

    private static Result<VersaoConfiguracao> Publicar(ProcessoSeletivo processo) => processo.Publicar(
        Dados(), "{}"u8.ToArray(), "1.1", "canonical-json/sha256@v1", HashFixo, "teste", TimeProvider.System, ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);

    /// <summary>
    /// Issue #1112: publicar sem declarar cobrança de taxa é recusado (CA-01) — declarada em
    /// todo teste deste arquivo para que a pendência de CRONOGRAMA isolada por cada um seja a
    /// única a aflorar, não mascarada pela pendência estrutural genérica.
    /// </summary>
    private static void DeclararNaoCobra(ProcessoSeletivo processo)
    {
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);
    }

    // ── CA-11 — piso mínimo de InscricaoPropria ──

    [Fact(DisplayName = "CA-11: origem InscricaoPropria sem NENHUMA fase que colete inscrição reprova a publicação")]
    public void InscricaoPropria_SemFaseDeColeta_Reprova()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente);
        processo.DefinirDistribuicaoVagas([Distribuicao(40)], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente);
        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.ClassificacaoImportada, 'b'), null, null,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'), [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: [], escalaNotaFinal: null).Value!, PrecondicaoIfMatch.Ausente);
        processo.DefinirCronogramaFases(
            [Fase(1, "RESULTADO_FINAL", produzResultado: true).Value!], [], PrecondicaoIfMatch.Ausente);
        DeclararNaoCobra(processo);

        Result<VersaoConfiguracao> resultado = Publicar(processo);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.InscricaoPropriaSemFaseDeColeta");
    }

    // ── Issue #851, CA-12 — indistinguibilidade por tipo ──

    public static IEnumerable<object[]> TiposDeProcessoDistintos =>
    [
        [TipoProcesso.SiSU],
        [TipoProcesso.PSIQ],
    ];

    [Theory(DisplayName = "Issue #851, CA-12: ImportacaoExterna publica sem inscrição/etapa/fórmula local — o veredicto NÃO muda com o tipo (indistinguibilidade)")]
    [MemberData(nameof(TiposDeProcessoDistintos))]
    public void ImportacaoExterna_MesmaConfiguracao_TiposDiferentes_MesmoVeredicto(TipoProcessoSnapshot tipo)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", tipo, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente);
        processo.DefinirDistribuicaoVagas([Distribuicao(40)], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente);
        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.ClassificacaoImportada, 'b'), null, null,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'), [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: [], escalaNotaFinal: null).Value!, PrecondicaoIfMatch.Ausente);
        processo.DefinirCronogramaFases(
            [Fase(1, "RESULTADO_FINAL", produzResultado: true).Value!], [], PrecondicaoIfMatch.Ausente);
        DeclararNaoCobra(processo);

        Result<VersaoConfiguracao> resultado = Publicar(processo);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    // ── CA-13 — vagas sem fase que produz resultado ──

    [Fact(DisplayName = "CA-13: havendo vagas ofertadas e NENHUMA fase que produz resultado, a publicação é recusada")]
    public void ComVagas_SemFaseQueProduzResultado_Reprova()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente);
        processo.DefinirDistribuicaoVagas([Distribuicao(40)], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente);
        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.ClassificacaoImportada, 'b'), null, null,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'), [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: [], escalaNotaFinal: null).Value!, PrecondicaoIfMatch.Ausente);
        // Só a fase de matrícula — nenhuma produz resultado.
        processo.DefinirCronogramaFases([Fase(1, "MATRICULA").Value!], [], PrecondicaoIfMatch.Ausente);
        DeclararNaoCobra(processo);

        Result<VersaoConfiguracao> resultado = Publicar(processo);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.VagasSemFaseQueProduzResultado");
    }

    // ── CA-14 — direção lazy: etapa sem fase de avaliação ──

    [Fact(DisplayName = "CA-14: etapa pontuada declarada e NENHUMA fase agrupa etapas reprova a publicação")]
    public void Etapa_SemFaseDeAvaliacao_Reprova()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        processo.DefinirEtapas(
            [EtapaProcesso.Criar("Prova", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, peso: 1m, ordem: 1, notaMaxima: 10m, formaDeAvaliacao: FormaDeAvaliacao.Objetiva).Value!], PrecondicaoIfMatch.Ausente);
        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente);
        processo.DefinirDistribuicaoVagas([Distribuicao(40)], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente);
        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.FormulaMediaPonderada, 'b'),
            Regra(RegraArredondamentoCodigo.PrecisaoTruncar, 'd'), 2,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'), [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: [], escalaNotaFinal: 100m).Value!, PrecondicaoIfMatch.Ausente);
        // Fase que produz resultado, mas NÃO agrupa etapas.
        processo.DefinirCronogramaFases([Fase(1, "RESULTADO_FINAL", produzResultado: true).Value!], [], PrecondicaoIfMatch.Ausente);
        DeclararNaoCobra(processo);

        Result<VersaoConfiguracao> resultado = Publicar(processo);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.EtapaSemFaseDeAvaliacao");
    }

    // ── CA-15 — DefinirEtapas([]) aceito; divisor > 0 só sob fórmula local ──

    [Fact(DisplayName = "CA-15 (regressão): processo SEM etapa, com CLASSIFICACAO-IMPORTADA, publica — hoje um processo sem etapa não publicava (o teste falha sem o fix)")]
    public void SemEtapa_ClassificacaoImportada_Publica()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS SiSU", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        processo.DefinirEtapas([], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue(
            "Story #851 §3.5: lista vazia é estado válido — um processo sem prova (SiSU) não tem etapa");
        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente);
        processo.DefinirDistribuicaoVagas([Distribuicao(40)], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente);
        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.ClassificacaoImportada, 'b'), null, null,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'), [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: [], escalaNotaFinal: null).Value!, PrecondicaoIfMatch.Ausente);
        processo.DefinirCronogramaFases([Fase(1, "RESULTADO_FINAL", produzResultado: true).Value!], [], PrecondicaoIfMatch.Ausente);
        DeclararNaoCobra(processo);

        Result<VersaoConfiguracao> resultado = Publicar(processo);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "CA-15: sob FORMULA-MEDIA-PONDERADA, divisor ZERO reprova a publicação (etapas só eliminatórias, sem peso)")]
    public void FormulaLocal_DivisorZero_Reprova()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        // Etapa eliminatória pura — NÃO compõe a nota (ComponeNota exige classificatória/ambas + peso).
        // O guard "NenhumaEtapaComponeNota" recusaria isso ANTES de chegarmos ao divisor — o
        // cenário aqui prova a MESMA classe de defeito por outro caminho: guard "vivo" via
        // reflection para simular um estado que hoje só seria alcançável por dado legado.
        Result definirEtapas = processo.DefinirEtapas(
            [EtapaProcesso.Criar("Entrevista", CaraterEtapa.Eliminatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, peso: null, notaMinima: 5m, ordem: 1, notaMaxima: 10m, formaDeAvaliacao: FormaDeAvaliacao.Objetiva).Value!],
            PrecondicaoIfMatch.Ausente);
        definirEtapas.IsFailure.Should().BeTrue(
            "pré-condição: DefinirEtapas já recusa uma lista sem NENHUMA etapa que componha a nota — " +
            "prova que o guard estrutural do domínio intercepta o divisor zero antes mesmo do gate de publicação");
        definirEtapas.Error!.Code.Should().Be("ProcessoSeletivo.NenhumaEtapaComponeNota");
    }

    // ── Tipo de ato único por objeto declarado mais de uma vez ──

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
            Dados(), "{}"u8.ToArray(), "1.1", "canonical-json/sha256@v1", HashFixo, "teste", TimeProvider.System,
            ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, unicos);

    private static FaseCronograma FaseQuePublica(int ordem, string codigo, params ProdutoDaFase[] produtos) =>
        FaseCronograma.Criar(
            ordem, Guid.CreateVersion7(), codigo, OrigemDataFase.NaExecucao,
            agrupaEtapas: false, permiteComplementacao: false,
            coletaInscricao: false, coletaSolicitacaoIsencao: false,
            inicio: null, fim: null,
            produtos: produtos,
            faseConcluinteCodigo: null, emiteParecerIndividual: false,
            bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;

    private static ProcessoSeletivo ProcessoPublicavelComCronograma(params FaseCronograma[] fases)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente);
        processo.DefinirDistribuicaoVagas([Distribuicao(40)], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente);
        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.ClassificacaoImportada, 'b'), null, null,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'), [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: [], escalaNotaFinal: null).Value!, PrecondicaoIfMatch.Ausente);
        processo.DefinirCronogramaFases(fases, [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        DeclararNaoCobra(processo);
        return processo;
    }

    [Fact(DisplayName = "Tipo único declarado em duas fases reprova a publicação e a mensagem nomeia o tipo e as duas fases")]
    public void TipoUnico_EmDuasFases_ReprovaNomeandoOTipoEAsFases()
    {
        ProcessoSeletivo processo = ProcessoPublicavelComCronograma(
            FaseQuePublica(1, "CLASSIFICACAO", ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)),
            FaseQuePublica(2, "HABILITACAO", ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)));

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
            FaseQuePublica(1, "HOMOLOGACAO",
                ProdutoDaFase.Criar("HOMOLOGACAO_INSCRICOES", PapelProdutoFase.Preliminar),
                ProdutoDaFase.Criar("HOMOLOGACAO_INSCRICOES", PapelProdutoFase.Definitivo)));

        DomainError? pendencia = processo.PendenciaDoTipoDeAtoUnico(
            Unicos("HOMOLOGACAO_INSCRICOES", "Homologação das inscrições"));

        pendencia.Should().NotBeNull();
        pendencia!.Message.Should().Contain("Homologação das inscrições").And.Contain("na fase 'HOMOLOGACAO' (2 vezes)");
    }

    [Fact(DisplayName = "Tipo único declarado sem papel e com papel reprova")]
    public void TipoUnico_SemPapelEComPapel_Reprova()
    {
        ProcessoSeletivo processo = ProcessoPublicavelComCronograma(
            FaseQuePublica(1, "HOMOLOGACAO", ProdutoDaFase.Criar("HOMOLOGACAO_INSCRICOES", null)),
            FaseQuePublica(2, "RESULTADO", ProdutoDaFase.Criar("HOMOLOGACAO_INSCRICOES", PapelProdutoFase.Definitivo)));

        DomainError? pendencia = processo.PendenciaDoTipoDeAtoUnico(
            Unicos("HOMOLOGACAO_INSCRICOES", "Homologação das inscrições"));

        pendencia.Should().NotBeNull();
        pendencia!.Code.Should().Be("ProcessoSeletivo.TipoDeAtoUnicoDeclaradoMaisDeUmaVez");
    }

    [Fact(DisplayName = "Tipo não único declarado como preliminar e como definitivo é aceito")]
    public void TipoNaoUnico_PreliminarEDefinitivo_Aceita()
    {
        ProcessoSeletivo processo = ProcessoPublicavelComCronograma(
            FaseQuePublica(1, "HOMOLOGACAO",
                ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar),
                ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)));

        processo.PendenciaDoTipoDeAtoUnico(Unicos("HOMOLOGACAO_INSCRICOES", "Homologação das inscrições")).Should().BeNull();
    }

    [Fact(DisplayName = "Tipo único declarado numa fase e numa etapa reprova: o produto da etapa também conta")]
    public void TipoUnico_NaFaseENaEtapa_Reprova()
    {
        ProcessoSeletivo processo = ProcessoPublicavelComCronograma(
            FaseQuePublica(1, "CLASSIFICACAO", ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)));
        EtapaProcesso etapa = EtapaProcesso.Criar(
            "Prova Objetiva", CaraterEtapa.Classificatoria,
            TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!,
            peso: 3m, ordem: 1, notaMaxima: 10m, formaDeAvaliacao: FormaDeAvaliacao.Objetiva).Value!;
        etapa.DefinirProdutos([ProdutoDaEtapa.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)]).IsSuccess.Should().BeTrue();
        processo.DefinirEtapas([etapa], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        DomainError? pendencia = processo.PendenciaDoTipoDeAtoUnico(
            Unicos("RESULTADO_CLASSIFICACAO", "Resultado da classificação"));

        pendencia.Should().NotBeNull();
        pendencia!.Message.Should().Contain("na fase 'CLASSIFICACAO' e na etapa 'Prova Objetiva' (ordem 1)");
    }

    [Fact(DisplayName = "Tipo não único declarado como preliminar e como definitivo é aceito e publica")]
    public void TipoNaoUnico_PreliminarEDefinitivo_Publica()
    {
        ProcessoSeletivo processo = ProcessoPublicavelComCronograma(
            FaseQuePublica(1, "HOMOLOGACAO",
                ProdutoDaFase.Criar("RESULTADO_HOMOLOGACAO", PapelProdutoFase.Preliminar),
                ProdutoDaFase.Criar("RESULTADO_HOMOLOGACAO", PapelProdutoFase.Definitivo)));

        Result<VersaoConfiguracao> resultado = PublicarComUnicos(processo, Unicos("RESULTADO_CLASSIFICACAO", "Resultado da classificação"));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "O checklist projeta o item do tipo único: vermelho com a duplicidade, verde sem ela")]
    public void Checklist_ProjetaOItemDoTipoUnico()
    {
        ProcessoSeletivo duplicado = ProcessoPublicavelComCronograma(
            FaseQuePublica(1, "CLASSIFICACAO", ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)),
            FaseQuePublica(2, "HABILITACAO", ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)));
        ProcessoSeletivo unico = ProcessoPublicavelComCronograma(
            FaseQuePublica(1, "CLASSIFICACAO", ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)));
        TiposDeAtoUnicosPorObjeto unicos = Unicos("RESULTADO_CLASSIFICACAO", "Resultado da classificação");

        duplicado.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, unicos)
            .Should().Contain(item => item.Codigo == CodigoItemTipoUnico && !item.Ok);
        unico.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, unicos)
            .Should().Contain(item => item.Codigo == CodigoItemTipoUnico && item.Ok);
    }

    [Fact(DisplayName = "O ato da própria publicação ocupa a vaga: produto do mesmo tipo único no cronograma reprova")]
    public void TipoUnico_NoAtoDaPublicacaoENoCronograma_Reprova()
    {
        ProcessoSeletivo processo = ProcessoPublicavelComCronograma(
            FaseQuePublica(1, "ABERTURA", ProdutoDaFase.Criar("EDITAL_ABERTURA", PapelProdutoFase.Definitivo)));

        Result<VersaoConfiguracao> resultado = PublicarComUnicos(
            processo, Unicos("EDITAL_ABERTURA", "Edital de abertura", atoDaOperacao: "EDITAL_ABERTURA"));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.TipoDeAtoUnicoDeclaradoMaisDeUmaVez");
        resultado.Error.Message.Should().Contain("na fase 'ABERTURA' e no ato que está sendo registrado");
    }

    [Fact(DisplayName = "Duas etapas de mesmo nome em ordens distintas são contadas cada uma, com a ordem na mensagem")]
    public void TipoUnico_EmDuasEtapasDeMesmoNome_ContaCadaDeclaracao()
    {
        ProcessoSeletivo processo = ProcessoPublicavelComCronograma(
            FaseQuePublica(1, "CLASSIFICACAO"));
        EtapaProcesso Etapa(int ordem)
        {
            EtapaProcesso etapa = EtapaProcesso.Criar(
                "Prova", CaraterEtapa.Classificatoria,
                TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!,
                peso: 3m, ordem: ordem, notaMaxima: 10m, formaDeAvaliacao: FormaDeAvaliacao.Objetiva).Value!;
            etapa.DefinirProdutos([ProdutoDaEtapa.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)]).IsSuccess.Should().BeTrue();
            return etapa;
        }

        processo.DefinirEtapas([Etapa(1), Etapa(2)], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        DomainError? pendencia = processo.PendenciaDoTipoDeAtoUnico(Unicos("RESULTADO_CLASSIFICACAO", "Resultado da classificação"));

        pendencia.Should().NotBeNull();
        pendencia!.Message.Should().Contain("na etapa 'Prova' (ordem 1) e na etapa 'Prova' (ordem 2)");
    }

    [Fact(DisplayName = "Retificação: o cronograma que passa a declarar o tipo único já ocupado por ato registrado do processo reprova")]
    public void TipoUnico_JaOcupadoPorAtoRegistrado_ReprovaProdutoDoCronograma()
    {
        ProcessoSeletivo processo = ProcessoPublicavelComCronograma(
            FaseQuePublica(1, "ABERTURA", ProdutoDaFase.Criar("EDITAL_ABERTURA", PapelProdutoFase.Definitivo)));

        DomainError? pendencia = processo.PendenciaDoTipoDeAtoUnico(
            Unicos("EDITAL_ABERTURA", "Edital de abertura", atoDaOperacao: "RETIFICACAO_EDITAL", vagaOcupada: true));

        pendencia.Should().NotBeNull();
        pendencia!.Message.Should().Contain("na fase 'ABERTURA' e no ato já registrado do processo");
    }

    [Fact(DisplayName = "Retificação com ato do mesmo tipo único da própria linhagem não conta duas vezes a mesma vaga")]
    public void TipoUnico_AtoDaOperacaoEAtoRegistradoDoMesmoTipo_ContamUmaVaga()
    {
        ProcessoSeletivo processo = ProcessoPublicavelComCronograma(
            FaseQuePublica(1, "CLASSIFICACAO", ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)));

        processo.PendenciaDoTipoDeAtoUnico(
            Unicos("EDITAL_ABERTURA", "Edital de abertura", atoDaOperacao: "EDITAL_ABERTURA", vagaOcupada: true))
            .Should().BeNull();
    }

    [Fact(DisplayName = "Só o catálogo vigente decide a unicidade: tipo ocupado por ato registrado que o catálogo não declara único não reprova")]
    public void TipoOcupadoQueOCatalogoNaoDeclaraUnico_NaoReprova()
    {
        ProcessoSeletivo processo = ProcessoPublicavelComCronograma(
            FaseQuePublica(1, "ABERTURA", ProdutoDaFase.Criar("EDITAL_ABERTURA", PapelProdutoFase.Definitivo)));
        TiposDeAtoUnicosPorObjeto ocupadoMasNaoUnico = TiposDeAtoUnicosPorObjeto.ParaOperacao(
            new Dictionary<string, string>(), ["EDITAL_ABERTURA"], "RETIFICACAO_EDITAL");

        processo.PendenciaDoTipoDeAtoUnico(ocupadoMasNaoUnico).Should().BeNull();
    }

    private static FaseCronograma FaseQueConclui(int ordem, string codigo, string? concluinte, params ProdutoDaFase[] produtos) =>
        FaseCronograma.Criar(
            ordem, Guid.CreateVersion7(), codigo, OrigemDataFase.NaExecucao,
            agrupaEtapas: false, permiteComplementacao: false,
            coletaInscricao: false, coletaSolicitacaoIsencao: false,
            inicio: null, fim: null,
            produtos: produtos,
            faseConcluinteCodigo: concluinte, emiteParecerIndividual: false,
            bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;

    [Fact(DisplayName = "Aviso: o mesmo tipo definitivo em duas fases gera o aviso, com o código estável e as duas fases na mensagem")]
    public void Aviso_MesmoDefinitivoEmDuasFases_GeraAviso()
    {
        ProcessoSeletivo processo = ProcessoPublicavelComCronograma(
            FaseQuePublica(1, "CLASSIFICACAO", ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)),
            FaseQuePublica(2, "HABILITACAO", ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)));

        AvisoConformidade aviso = processo.AvaliarAvisos().Should().ContainSingle().Subject;

        aviso.Codigo.Should().Be(CodigosDeAvisoDeConformidade.CronogramaTipoDefinitivoEmMaisDeUmLugar);
        aviso.Dimensao.Should().Be(DimensaoConformidade.Cronograma);
        aviso.Mensagem.Should().Contain("'RESULTADO_CLASSIFICACAO'").And.Contain("na fase 'CLASSIFICACAO' e na fase 'HABILITACAO'");
    }

    [Fact(DisplayName = "Aviso: o mesmo tipo definitivo numa fase e numa etapa gera o aviso nomeando a etapa")]
    public void Aviso_MesmoDefinitivoNaFaseENaEtapa_GeraAviso()
    {
        ProcessoSeletivo processo = ProcessoPublicavelComCronograma(
            FaseQuePublica(1, "CLASSIFICACAO", ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)));
        EtapaProcesso etapa = EtapaProcesso.Criar(
            "Prova Objetiva", CaraterEtapa.Classificatoria,
            TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!,
            peso: 3m, ordem: 1, notaMaxima: 10m, formaDeAvaliacao: FormaDeAvaliacao.Objetiva).Value!;
        etapa.DefinirProdutos([ProdutoDaEtapa.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)]).IsSuccess.Should().BeTrue();
        processo.DefinirEtapas([etapa], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.AvaliarAvisos().Should().ContainSingle().Which.Mensagem
            .Should().Contain("na fase 'CLASSIFICACAO' e na etapa 'Prova Objetiva' (ordem 1)");
    }

    [Fact(DisplayName = "Aviso: o tipo definitivo em um lugar só, ainda que com preliminar, não gera aviso")]
    public void Aviso_DefinitivoEmUmLugarSo_NaoGeraAviso()
    {
        ProcessoSeletivo processo = ProcessoPublicavelComCronograma(
            FaseQuePublica(1, "HOMOLOGACAO",
                ProdutoDaFase.Criar("RESULTADO_HOMOLOGACAO", PapelProdutoFase.Preliminar),
                ProdutoDaFase.Criar("RESULTADO_HOMOLOGACAO", PapelProdutoFase.Definitivo)));

        processo.AvaliarAvisos().Should().BeEmpty();
    }

    [Fact(DisplayName = "Aviso: um único definitivo da fase concluinte que fecha dois ciclos não gera aviso")]
    public void Aviso_UmDefinitivoQueFechaDoisCiclos_NaoGeraAviso()
    {
        ProcessoSeletivo processo = ProcessoPublicavelComCronograma(
            FaseQueConclui(1, "ANALISE_A", "CONCLUSAO", ProdutoDaFase.Criar("PARECER", PapelProdutoFase.Preliminar)),
            FaseQueConclui(2, "ANALISE_B", "CONCLUSAO", ProdutoDaFase.Criar("PARECER", PapelProdutoFase.Preliminar)),
            FaseQueConclui(3, "CONCLUSAO", null, ProdutoDaFase.Criar("PARECER", PapelProdutoFase.Definitivo)));

        processo.AvaliarAvisos().Should().BeEmpty();
    }

    [Fact(DisplayName = "Aviso nunca impede a publicação: o processo com o aviso publica e o checklist não tem item vermelho por causa dele")]
    public void Aviso_NaoImpedeAPublicacao()
    {
        ProcessoSeletivo processo = ProcessoPublicavelComCronograma(
            FaseQuePublica(1, "CLASSIFICACAO", ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)),
            FaseQuePublica(2, "HABILITACAO", ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)));

        processo.AvaliarAvisos().Should().NotBeEmpty();
        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum)
            .Where(static i => i.Codigo.StartsWith("cronograma_", StringComparison.Ordinal)).Should().OnlyContain(static i => i.Ok);
        PublicarComUnicos(processo, TiposDeAtoUnicosDeTeste.Nenhum).IsSuccess.Should().BeTrue();
    }
}
