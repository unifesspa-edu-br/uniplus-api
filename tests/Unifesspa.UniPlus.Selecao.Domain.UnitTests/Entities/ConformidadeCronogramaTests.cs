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
            ordem, Guid.CreateVersion7(), codigo, "CEPS", OrigemDataFase.Delegada,
            agrupaEtapas, permiteComplementacao: false,
            coletaInscricao, coletaSolicitacaoIsencao,
            inicio: null, fim: null,
            produtos: produzResultado ? [ProdutoDaFase.Criar(codigo, PapelProdutoFase.Definitivo)] : [],
            faseConcluinteCodigo: null, emiteParecerIndividual: false,
            bancasRequeridas: [], regraRecurso: null);

    private static DadosEdital Dados() => DadosEdital.Criar(
        "001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)), new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)), Guid.CreateVersion7()).Value!;

    private static Result<VersaoConfiguracao> Publicar(ProcessoSeletivo processo) => processo.Publicar(
        Dados(), "{}"u8.ToArray(), "1.1", "canonical-json/sha256@v1", HashFixo, "teste", TimeProvider.System, ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo);

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
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'), [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: []).Value!, PrecondicaoIfMatch.Ausente);
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
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'), [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: []).Value!, PrecondicaoIfMatch.Ausente);
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
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'), [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: []).Value!, PrecondicaoIfMatch.Ausente);
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
            [EtapaProcesso.Criar("Prova", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, peso: 1m, ordem: 1).Value!], PrecondicaoIfMatch.Ausente);
        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente);
        processo.DefinirDistribuicaoVagas([Distribuicao(40)], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente);
        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.FormulaMediaPonderada, 'b'),
            Regra(RegraArredondamentoCodigo.PrecisaoTruncar, 'd'), 2,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'), [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: []).Value!, PrecondicaoIfMatch.Ausente);
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
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'), [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: []).Value!, PrecondicaoIfMatch.Ausente);
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
            [EtapaProcesso.Criar("Entrevista", CaraterEtapa.Eliminatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, peso: null, notaMinima: 5m, ordem: 1).Value!],
            PrecondicaoIfMatch.Ausente);
        definirEtapas.IsFailure.Should().BeTrue(
            "pré-condição: DefinirEtapas já recusa uma lista sem NENHUMA etapa que componha a nota — " +
            "prova que o guard estrutural do domínio intercepta o divisor zero antes mesmo do gate de publicação");
        definirEtapas.Error!.Code.Should().Be("ProcessoSeletivo.NenhumaEtapaComponeNota");
    }

    // ── Tipo de ato único por objeto declarado mais de uma vez ──

    private const string CodigoItemTipoUnico = "cronograma_tipo_de_ato_unico_declarado_uma_vez";

    private static ContextoDeContagemDePrazos ContextoComUnico(string codigo, string nome) =>
        ContextoDeContagemDePrazos.SemCalendario with
        {
            TiposDeAtoUnicos = new TiposDeAtoUnicosPorObjeto(new Dictionary<string, string> { [codigo] = nome }),
        };

    private static FaseCronograma FaseQuePublica(int ordem, string codigo, params ProdutoDaFase[] produtos) =>
        FaseCronograma.Criar(
            ordem, Guid.CreateVersion7(), codigo, "CEPS", OrigemDataFase.Delegada,
            agrupaEtapas: false, permiteComplementacao: false,
            coletaInscricao: false, coletaSolicitacaoIsencao: false,
            inicio: null, fim: null,
            produtos: produtos,
            faseConcluinteCodigo: null, emiteParecerIndividual: false,
            bancasRequeridas: [], regraRecurso: null).Value!;

    private static ProcessoSeletivo ProcessoPublicavelComCronograma(params FaseCronograma[] fases)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente);
        processo.DefinirDistribuicaoVagas([Distribuicao(40)], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente);
        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.ClassificacaoImportada, 'b'), null, null,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'), [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: []).Value!, PrecondicaoIfMatch.Ausente);
        processo.DefinirCronogramaFases(fases, [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        DeclararNaoCobra(processo);
        return processo;
    }

    [Fact(DisplayName = "Tipo único declarado em duas fases reprova a publicação e a mensagem nomeia o tipo e as duas fases")]
    public void TipoUnico_EmDuasFases_ReprovaNomeandoOTipoEAsFases()
    {
        ProcessoSeletivo processo = ProcessoPublicavelComCronograma(
            FaseQuePublica(1, "CLASSIFICACAO", ProdutoDaFase.Criar("RESULTADO_FINAL", PapelProdutoFase.Definitivo)),
            FaseQuePublica(2, "HABILITACAO", ProdutoDaFase.Criar("RESULTADO_FINAL", PapelProdutoFase.Definitivo)));

        Result<VersaoConfiguracao> resultado = processo.Publicar(
            Dados(), "{}"u8.ToArray(), "1.1", "canonical-json/sha256@v1", HashFixo, "teste", TimeProvider.System,
            ContextoComUnico("RESULTADO_FINAL", "Resultado final"), FatosDeModalidadeDeTeste.DoCatalogo);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.TipoDeAtoUnicoDeclaradoMaisDeUmaVez");
        resultado.Error.Message.Should().Be(
            "O tipo de ato 'Resultado final' só pode ser publicado uma vez por processo e está declarado na fase 'CLASSIFICACAO' e na fase 'HABILITACAO'.");
    }

    [Fact(DisplayName = "Tipo único declarado como preliminar e como definitivo na mesma fase reprova")]
    public void TipoUnico_NaMesmaFaseComDoisPapeis_Reprova()
    {
        ProcessoSeletivo processo = ProcessoPublicavelComCronograma(
            FaseQuePublica(1, "HOMOLOGACAO",
                ProdutoDaFase.Criar("HOMOLOGACAO_INSCRICOES", PapelProdutoFase.Preliminar),
                ProdutoDaFase.Criar("HOMOLOGACAO_INSCRICOES", PapelProdutoFase.Definitivo)));

        DomainError? pendencia = processo.PendenciaDoTipoDeAtoUnico(
            ContextoComUnico("HOMOLOGACAO_INSCRICOES", "Homologação das inscrições").TiposUnicos);

        pendencia.Should().NotBeNull();
        pendencia!.Message.Should().Contain("Homologação das inscrições").And.Contain("mais de uma vez na fase 'HOMOLOGACAO'");
    }

    [Fact(DisplayName = "Tipo único declarado numa fase e numa etapa reprova: o produto da etapa também conta")]
    public void TipoUnico_NaFaseENaEtapa_Reprova()
    {
        ProcessoSeletivo processo = ProcessoPublicavelComCronograma(
            FaseQuePublica(1, "CLASSIFICACAO", ProdutoDaFase.Criar("RESULTADO_FINAL", PapelProdutoFase.Definitivo)));
        EtapaProcesso etapa = EtapaProcesso.Criar(
            "Prova Objetiva", CaraterEtapa.Classificatoria,
            TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!,
            peso: 3m, ordem: 1).Value!;
        etapa.DefinirProdutos([ProdutoDaEtapa.Criar("RESULTADO_FINAL", PapelProdutoFase.Definitivo)]).IsSuccess.Should().BeTrue();
        processo.DefinirEtapas([etapa], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        DomainError? pendencia = processo.PendenciaDoTipoDeAtoUnico(
            ContextoComUnico("RESULTADO_FINAL", "Resultado final").TiposUnicos);

        pendencia.Should().NotBeNull();
        pendencia!.Message.Should().Contain("na fase 'CLASSIFICACAO' e na etapa 'Prova Objetiva'");
    }

    [Fact(DisplayName = "Tipo não único declarado como preliminar e como definitivo é aceito e publica")]
    public void TipoNaoUnico_PreliminarEDefinitivo_Publica()
    {
        ProcessoSeletivo processo = ProcessoPublicavelComCronograma(
            FaseQuePublica(1, "HOMOLOGACAO",
                ProdutoDaFase.Criar("RESULTADO_HOMOLOGACAO", PapelProdutoFase.Preliminar),
                ProdutoDaFase.Criar("RESULTADO_HOMOLOGACAO", PapelProdutoFase.Definitivo)));

        Result<VersaoConfiguracao> resultado = processo.Publicar(
            Dados(), "{}"u8.ToArray(), "1.1", "canonical-json/sha256@v1", HashFixo, "teste", TimeProvider.System,
            ContextoComUnico("RESULTADO_FINAL", "Resultado final"), FatosDeModalidadeDeTeste.DoCatalogo);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "O checklist projeta o item do tipo único: vermelho com a duplicidade, verde sem ela")]
    public void Checklist_ProjetaOItemDoTipoUnico()
    {
        ProcessoSeletivo duplicado = ProcessoPublicavelComCronograma(
            FaseQuePublica(1, "CLASSIFICACAO", ProdutoDaFase.Criar("RESULTADO_FINAL", PapelProdutoFase.Definitivo)),
            FaseQuePublica(2, "HABILITACAO", ProdutoDaFase.Criar("RESULTADO_FINAL", PapelProdutoFase.Definitivo)));
        ProcessoSeletivo unico = ProcessoPublicavelComCronograma(
            FaseQuePublica(1, "CLASSIFICACAO", ProdutoDaFase.Criar("RESULTADO_FINAL", PapelProdutoFase.Definitivo)));
        ContextoDeContagemDePrazos contexto = ContextoComUnico("RESULTADO_FINAL", "Resultado final");

        duplicado.AvaliarConformidade(contexto, FatosDeModalidadeDeTeste.DoCatalogo)
            .Should().Contain(item => item.Codigo == CodigoItemTipoUnico && !item.Ok);
        unico.AvaliarConformidade(contexto, FatosDeModalidadeDeTeste.DoCatalogo)
            .Should().Contain(item => item.Codigo == CodigoItemTipoUnico && item.Ok);
    }
}
