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
    [Fact(DisplayName = "issue #563 (CA-13, bloco default/D5): restaurar repõe ausência mesmo que a sessão editorial tenha gravado uma configuração explícita")]
    public void Restauracao_RepoeAusenciaDeConfiguracaoDivulgacaoQuandoOGrafoCongeladoENulo()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);

        processo.AbrirRetificacao("Editar a divulgação", versao, identificadorDaVersaoBase: null, VersoesPublicadasDeTeste.SoAVigente(processo), "testes", DateTimeOffset.UnixEpoch)
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

        processo.AbrirRetificacao("Trocar a fase da Ordem 1", versao, identificadorDaVersaoBase: null, VersoesPublicadasDeTeste.SoAVigente(processo), "testes", DateTimeOffset.UnixEpoch)
            .IsSuccess.Should().BeTrue();

        // A sessão editorial troca a fase da Ordem 1 por uma fase GENUINAMENTE diferente
        // (FaseCanonicaOrigemId novo, não reaproveita a identidade estável da fase
        // publicada) — DefinirCronogramaFases reconcilia por FaseCanonicaOrigemId (CA-04),
        // então isto insere uma instância NOVA em vez de atualizar a existente no lugar.
        FaseCronograma faseTrocada = FaseCronograma.Criar(
            ordem: 1,
            faseCanonicaOrigemId: Guid.CreateVersion7(),
            codigo: "RESULTADO_FINAL",
            origemData: OrigemDataFase.NoEdital,
            agrupaEtapas: true,
            permiteComplementacao: false,
            coletaInscricao: false, coletaSolicitacaoIsencao: false,
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            produtos: [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)],
            faseConcluinteCodigo: null,
            emiteParecerIndividual: false,
            bancasRequeridas: [],
            regraRecurso: null,
            unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;
        processo.DefinirCronogramaFases([faseTrocada], [], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        Guid faseVivaId = processo.CronogramaFases.Single().Id;

        // O grafo CONGELADO referencia a fase que existia QUANDO a versão foi publicada —
        // um Id diferente do da fase viva acima (Reidratar preserva o Id congelado no
        // envelope 1.2, ADR-0110 D2), mas na MESMA Ordem — o caso que a reconciliação por
        // Ordem de AplicarGrafo reusa a instância viva em vez da decodificada.
        Guid faseCongeladaId = Guid.CreateVersion7();
        FaseCronograma faseCongelada = FaseCronograma.Reidratar(
            faseCongeladaId, ordem: 1, faseCanonicaOrigemId: Guid.CreateVersion7(), codigo: "RESULTADO_FINAL",
            origemData: OrigemDataFase.NoEdital, agrupaEtapas: true,
            permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false,
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            produtos: [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)],
            faseConcluinteCodigo: null,
            emiteParecerIndividual: false,
            bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps);

        DocumentoExigido documentoCongelado = DocumentoExigido.Reidratar(
            Guid.CreateVersion7(), exigidoNaFaseId: faseCongeladaId, tipoDocumentoOrigemId: Guid.CreateVersion7(),
            tipoDocumentoCodigo: "IDENTIDADE", tipoDocumentoNome: "Documento de identidade",
            tipoDocumentoCategoria: "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal, aplicabilidade: Aplicabilidade.Geral, obrigatorio: true,
            consequenciaIndeferimento: null, condicoes: [], basesLegais: [],
            idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null);

        ReferenciaTemporalFatos referenciaCongelada = ReferenciaTemporalFatos.Criar(
            ReferenciaTipo.FimFase, null, faseCongeladaId).Value!;

        GrafoConfiguracao grafoCongelado = new(
            etapas: [EtapaProcesso.Reidratar(EtapaCongelada, "Prova", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, 1m, null, 1, notaMaxima: 10m, formaDeAvaliacao: FormaDeAvaliacao.Objetiva)],
            ofertaAtendimento: OfertaAtendimentoEspecializado.Criar([], [], []).Value!,
            distribuicaoVagas: [Distribuicao()],
            bonusRegional: null,
            criteriosDesempate: [],
            classificacao: Classificacao([]),
            cronogramaFases: [faseCongelada],
            documentosExigidos: [documentoCongelado],
            nosExigencia: [],
            referenciaTemporalFatos: referenciaCongelada,
            numeroDeOpcoesDeCurso: 1);

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

        processo.DefinirCronogramaFases([FaseConforme()], [], PrecondicaoIfMatch.Ausente);
        processo.DefinirEtapas([
            EtapaProcesso.Reidratar(EtapaOriginal, "Prova Original", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, 1m, null, 1, notaMaxima: 10m, formaDeAvaliacao: FormaDeAvaliacao.Objetiva, faseCodigo: "RESULTADO_FINAL"),
        ], PrecondicaoIfMatch.Ausente);
        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente);
        processo.DefinirDistribuicaoVagas([Distribuicao()], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente);
        processo.DefinirClassificacao(Classificacao([]), PrecondicaoIfMatch.Ausente);

        // Issue #1112: publicar sem declarar cobrança de taxa é recusado (CA-01).
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!,
            PrecondicaoIfMatch.Ausente);
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);

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
            clock: TimeProvider.System, ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum).IsSuccess.Should().BeTrue();

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

    private static GrafoConfiguracao GrafoComBonus(ConfiguracaoBonusRegional bonus) => new(
        etapas: [EtapaProcesso.Reidratar(EtapaCongelada, "Prova", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, 1m, null, 1, notaMaxima: 10m, formaDeAvaliacao: FormaDeAvaliacao.Objetiva)],
        ofertaAtendimento: OfertaAtendimentoEspecializado.Criar([], [], []).Value!,
        distribuicaoVagas: [Distribuicao()],
        bonusRegional: bonus,
        criteriosDesempate: [],
        classificacao: Classificacao([]),
        cronogramaFases: [FaseConforme()],
        documentosExigidos: [],
        nosExigencia: [],
        referenciaTemporalFatos: null,
        numeroDeOpcoesDeCurso: 1);

    private static GrafoConfiguracao Grafo(
        IReadOnlyList<EtapaProcesso>? etapas = null,
        IReadOnlyList<CriterioDesempate>? criterios = null,
        IReadOnlyList<RegraEliminacao>? eliminacoes = null,
        IReadOnlyList<FaseCronograma>? cronogramaFases = null,
        ConfiguracaoDivulgacao? configuracaoDivulgacao = null,
        ConfiguracaoClassificacao? classificacao = null,
        IReadOnlyList<FatoColetado>? fatosColetados = null,
        int? numeroDeOpcoesDeCurso = 1) => new(
            etapas: etapas ?? [EtapaProcesso.Reidratar(EtapaCongelada, "Prova", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, 1m, null, 1, notaMaxima: 10m, formaDeAvaliacao: FormaDeAvaliacao.Objetiva)],
            ofertaAtendimento: OfertaAtendimentoEspecializado.Criar([], [], []).Value!,
            distribuicaoVagas: [Distribuicao()],
            bonusRegional: null,
            criteriosDesempate: criterios ?? [],
            classificacao: classificacao ?? Classificacao(eliminacoes ?? []),
            cronogramaFases: cronogramaFases ?? [FaseConforme()],
            documentosExigidos: [],
            nosExigencia: [],
            referenciaTemporalFatos: null,
            configuracaoDivulgacao: configuracaoDivulgacao,
            fatosColetados: fatosColetados,
            numeroDeOpcoesDeCurso: numeroDeOpcoesDeCurso);

    /// <summary>
    /// Mesmo grafo de <see cref="Grafo"/>, com <c>documentosExigidos</c>/<c>nosExigencia</c>
    /// explícitos — usado pelos testes de árvore de satisfação (Story #923), que precisam de
    /// uma fase congelada específica para <paramref name="documentosExigidos"/> referenciar.
    /// </summary>
    private static GrafoConfiguracao GrafoComArvore(
        FaseCronograma fase, IReadOnlyList<DocumentoExigido> documentosExigidos, IReadOnlyList<NoExigencia> nosExigencia) => new(
            etapas: [EtapaProcesso.Reidratar(EtapaCongelada, "Prova", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, 1m, null, 1, notaMaxima: 10m, formaDeAvaliacao: FormaDeAvaliacao.Objetiva)],
            ofertaAtendimento: OfertaAtendimentoEspecializado.Criar([], [], []).Value!,
            distribuicaoVagas: [Distribuicao()],
            bonusRegional: null,
            criteriosDesempate: [],
            classificacao: Classificacao([]),
            cronogramaFases: [fase],
            documentosExigidos: documentosExigidos,
            nosExigencia: nosExigencia,
            referenciaTemporalFatos: null,
            numeroDeOpcoesDeCurso: 1);

    private static DocumentoExigido DocumentoQualquer(Guid faseId) => DocumentoExigido.Criar(
        faseId, Guid.CreateVersion7(), "IDENTIDADE", "Documento de identidade", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal,
        Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null, [], [], null,
        FormatosPermitidos.Criar(true, null).Value!, null).Value!;

    /// <summary>Uma fase mínima e conforme: agrupa etapas (há 1 etapa por padrão) e produz resultado (há vagas por padrão).</summary>
    private static FaseCronograma FaseConforme() => FaseCronograma.Criar(
        ordem: 1,
        faseCanonicaOrigemId: new Guid("eeee0000-0000-4000-8000-000000000001"),
        codigo: "RESULTADO_FINAL",
        origemData: OrigemDataFase.NoEdital,
        agrupaEtapas: true,
        permiteComplementacao: false,
        coletaInscricao: false, coletaSolicitacaoIsencao: false,
        inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
        produtos: [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)],
        faseConcluinteCodigo: null,
        emiteParecerIndividual: false,
        bancasRequeridas: [],
        regraRecurso: null,
        unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;

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
                    RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), null, "Res. Unifesspa 532/2021", quantidadeDeclarada: 40).Value!,
            ], ofertaCurso: OfertaCursoDeTeste.Medicina).Value!;

    private static ConfiguracaoClassificacao Classificacao(IReadOnlyList<RegraEliminacao> eliminacoes) =>
        ConfiguracaoClassificacao.Criar(
            regraCalculo: Regra(RegraCalculoCodigo.FormulaMediaPonderada, 'b'),
            regraArredondamento: Regra(RegraArredondamentoCodigo.PrecisaoTruncar, 'c'),
            casasArredondamento: 2,
            regraOrdemAlocacao: Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'd'),
            regrasEliminacao: eliminacoes,
            baseadoEmEnem: false,
            resolucaoPesoAreaEnem: null,
            quadroPesoAreaEnem: [], escalaNotaFinal: 100m).Value!;

    private static ConfiguracaoClassificacao ClassificacaoEnemMediaPonderada(IReadOnlyList<RegraEliminacao>? eliminacoes = null) =>
        ConfiguracaoClassificacao.Criar(
            regraCalculo: Regra(RegraCalculoCodigo.FormulaMediaPonderada, 'b'),
            regraArredondamento: Regra(RegraArredondamentoCodigo.PrecisaoTruncar, 'c'),
            casasArredondamento: 2,
            regraOrdemAlocacao: Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'd'),
            regrasEliminacao: eliminacoes ?? [],
            baseadoEmEnem: true,
            resolucaoPesoAreaEnem: QuadroPesoAreaEnemDeTeste.Resolucao,
            quadroPesoAreaEnem: QuadroPesoAreaEnemDeTeste.Completo(), escalaNotaFinal: 100m).Value!;

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
