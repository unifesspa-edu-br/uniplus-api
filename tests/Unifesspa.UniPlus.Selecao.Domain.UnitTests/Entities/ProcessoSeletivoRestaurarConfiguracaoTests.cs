namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

/// <summary>
/// Reposição da configuração congelada (Story #859 CA-07; ADR-0110 D2).
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

    [Fact(DisplayName = "CA-07 — uma restauração que falha na ÚLTIMA validação não altera NADA")]
    public void RestauracaoQueFalha_NaoAlteraEstado()
    {
        // A falha é TARDIA de propósito: a classificação é a última dimensão validada.
        // Um caso trivial (etapas vazias) passaria mesmo numa implementação que aplicasse
        // etapas e distribuição antes de chegar à classificação — e não testaria nada.
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.PSIQ);
        VersaoConfiguracao versao = VersaoDo(processo);

        Estado antes = Estado.De(processo);

        // PSIQ não é baseado em ENEM — ELIM-CORTE-REDACAO não se aplica (INV-B13).
        GrafoConfiguracao invalido = Grafo(
            etapas: [EtapaProcesso.Reidratar(EtapaCongelada, "Prova", CaraterEtapa.Classificatoria, 1m, null, 1)],
            eliminacoes: [
                RegraEliminacao.Criar(
                    Regra(RegraEliminacaoCodigo.ElimCorteRedacao, 'e'),
                    new ArgsElimCorteRedacao(400m)).Value!,
            ]);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, invalido);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.EliminacaoEnemForaDeProcessoEnem");

        Estado.De(processo).Should().BeEquivalentTo(antes,
            "a validação acontece INTEIRA antes de qualquer escrita. Se a reposição aplicasse dimensão a dimensão, " +
            "este grafo já teria trocado etapas e distribuição antes de falhar na classificação — e o certame " +
            "ficaria numa configuração que nunca existiu.");
    }

    [Fact(DisplayName = "CA-07 — etapaRef órfão também falha sem tocar no estado")]
    public void EtapaRefOrfao_NaoAlteraEstado()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);
        Estado antes = Estado.De(processo);

        GrafoConfiguracao invalido = Grafo(
            etapas: [EtapaProcesso.Reidratar(EtapaCongelada, "Prova", CaraterEtapa.Classificatoria, 1m, null, 1)],
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
            EtapaProcesso.Reidratar(EtapaCongelada, "Prova A", CaraterEtapa.Classificatoria, 1m, null, 1),
            EtapaProcesso.Reidratar(EtapaCongelada, "Prova B", CaraterEtapa.Classificatoria, 2m, null, 2),
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

    [Fact(DisplayName = "D2 — a etapa que sobrevive é RECONCILIADA na mesma instância (o CreatedAt não se perde)")]
    public void EtapaSobrevivente_EReconciliada()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);

        EtapaProcesso instanciaViva = processo.Etapas.Single();

        // A etapa congelada tem o MESMO Id da viva, mas dados diferentes.
        GrafoConfiguracao grafo = Grafo(etapas: [
            EtapaProcesso.Reidratar(EtapaOriginal, "Nome Restaurado", CaraterEtapa.Ambas, 7m, 20m, 3),
        ]);

        processo.RestaurarConfiguracaoCongelada(versao, grafo).IsSuccess.Should().BeTrue();

        EtapaProcesso depois = processo.Etapas.Single();

        depois.Should().BeSameAs(instanciaViva,
            "substituir a instância tracked por outra com o mesmo Id colide com o identity map do EF — e o CreatedAt " +
            "original se perderia. A etapa é atualizada NA MESMA instância (ADR-0110 D2).");
        depois.Nome.Should().Be("Nome Restaurado", "os dados vêm do grafo congelado, não da instância viva");
        depois.Peso.Should().Be(7m);
        depois.Ordem.Should().Be(3);
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
            etapas: [EtapaProcesso.Reidratar(EtapaCongelada, "Prova", CaraterEtapa.Classificatoria, 1m, null, 1)],
            ofertaAtendimento: OfertaAtendimentoEspecializado.Criar([], [], []).Value!,
            distribuicaoVagas: [ofertaA, ofertaB],
            bonusRegional: null,
            criteriosDesempate: [],
            classificacao: Classificacao([]),
            cronogramaFases: [FaseConforme()],
            documentosExigidos: [],
            referenciaTemporalFatos: null);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, invalido);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.AcaoQuandoIndeferidoDivergente");
        Estado.De(processo).Should().BeEquivalentTo(antes,
            "a restauração recusada não pode deixar o agregado meio-reposto (CA-07)");
    }

    [Fact(DisplayName = "D2 — a etapa que NÃO existe mais é reinserida com o Id congelado")]
    public void EtapaAusente_EReinseridaComOIdCongelado()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);

        GrafoConfiguracao grafo = Grafo(etapas: [
            EtapaProcesso.Reidratar(EtapaCongelada, "Etapa Que Voltou", CaraterEtapa.Classificatoria, 1m, null, 1),
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

        processo.AbrirRetificacao("Incluir exigência documental", versao, "testes", DateTimeOffset.UnixEpoch)
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
            grupoSatisfacaoId: null,
            condicoes: [], basesLegais: [], idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;
        processo.DefinirDocumentosExigidos([exigencia], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();
        processo.DocumentosExigidos.Should().ContainSingle();

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, Grafo());

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.DocumentosExigidos.Should().BeEmpty(
            "a versão congelada nunca poderia ter sido publicada com exigência configurada (B-01) — restaurar " +
            "precisa repor esse estado vazio, não preservar o que a sessão descartada editou");
    }

    [Fact(DisplayName = "Story #554/issue #892 (achado Codex P1) — restauração limpa ReferenciaTemporalFatos definida durante a sessão")]
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

        processo.AbrirRetificacao("Ajustar referência temporal", versao, "testes", DateTimeOffset.UnixEpoch)
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

    [Fact(DisplayName = "Story #554, PR #903 (achado de revisão P2) — restaurar remapeia ExigidoNaFaseId/ReferenciaTemporalFatos.FaseId para a fase VIVA quando a sessão editorial trocou a fase da mesma Ordem")]
    public void Restauracao_RemapeiaReferenciasDeFaseParaAInstanciaViva()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);

        processo.AbrirRetificacao("Trocar a fase da Ordem 1", versao, "testes", DateTimeOffset.UnixEpoch)
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
            produzResultado: true,
            resultadoDefinitivo: true,
            coletaInscricao: false,
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            atoProduzidoCodigo: "RESULTADO_FINAL",
            atoProduzidoEfeitoIrreversivel: false,
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
            permiteComplementacao: false, produzResultado: true, resultadoDefinitivo: true, coletaInscricao: false,
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            atoProduzidoCodigo: "RESULTADO_FINAL", atoProduzidoEfeitoIrreversivel: false,
            bancasRequeridas: [], regraRecurso: null);

        DocumentoExigido documentoCongelado = DocumentoExigido.Reidratar(
            Guid.CreateVersion7(), exigidoNaFaseId: faseCongeladaId, tipoDocumentoOrigemId: Guid.CreateVersion7(),
            tipoDocumentoCodigo: "IDENTIDADE", tipoDocumentoNome: "Documento de identidade",
            tipoDocumentoCategoria: "PESSOAL", aplicabilidade: Aplicabilidade.Geral, obrigatorio: true,
            consequenciaIndeferimento: null, grupoSatisfacaoId: null, condicoes: [], basesLegais: [],
            idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null);

        ReferenciaTemporalFatos referenciaCongelada = ReferenciaTemporalFatos.Criar(
            ReferenciaTipo.FimFase, null, faseCongeladaId).Value!;

        GrafoConfiguracao grafoCongelado = new(
            etapas: [EtapaProcesso.Reidratar(EtapaCongelada, "Prova", CaraterEtapa.Classificatoria, 1m, null, 1)],
            ofertaAtendimento: OfertaAtendimentoEspecializado.Criar([], [], []).Value!,
            distribuicaoVagas: [Distribuicao()],
            bonusRegional: null,
            criteriosDesempate: [],
            classificacao: Classificacao([]),
            cronogramaFases: [faseCongelada],
            documentosExigidos: [documentoCongelado],
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

    private static ReferenciaRegra Regra(string codigo, char semente) =>
        ReferenciaRegra.Criar(codigo, "v1", new string(semente, 64)).Value!;

    private static ProcessoSeletivo ProcessoConforme(TipoProcesso tipo)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Restauração", tipo, OrigemCandidatos.ImportacaoExterna);

        processo.DefinirEtapas([
            EtapaProcesso.Reidratar(EtapaOriginal, "Prova Original", CaraterEtapa.Classificatoria, 1m, null, 1),
        ], PrecondicaoIfMatch.Ausente);
        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente);
        processo.DefinirDistribuicaoVagas([Distribuicao()], PrecondicaoIfMatch.Ausente);
        processo.DefinirClassificacao(Classificacao([]), PrecondicaoIfMatch.Ausente);
        processo.DefinirCronogramaFases([FaseConforme()], [], PrecondicaoIfMatch.Ausente);

        return processo;
    }

    private static ProcessoSeletivo ProcessoPublicado(TipoProcesso tipo)
    {
        ProcessoSeletivo processo = ProcessoConforme(tipo);

        processo.Publicar(
            Dados(),
            configuracaoCongeladaCanonica: [1, 2, 3],
            schemaVersion: "1.1",
            algoritmoHash: "canonical-json/sha256@v1",
            hashDocumento: new string('a', 64),
            atorUsuarioSub: "testes",
            clock: TimeProvider.System).IsSuccess.Should().BeTrue();

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
        IReadOnlyList<FaseCronograma>? cronogramaFases = null) => new(
            etapas: etapas ?? [EtapaProcesso.Reidratar(EtapaCongelada, "Prova", CaraterEtapa.Classificatoria, 1m, null, 1)],
            ofertaAtendimento: OfertaAtendimentoEspecializado.Criar([], [], []).Value!,
            distribuicaoVagas: [Distribuicao()],
            bonusRegional: null,
            criteriosDesempate: criterios ?? [],
            classificacao: Classificacao(eliminacoes ?? []),
            cronogramaFases: cronogramaFases ?? [FaseConforme()],
            documentosExigidos: [],
            referenciaTemporalFatos: null);

    /// <summary>Uma fase mínima e conforme: agrupa etapas (há 1 etapa por padrão) e produz resultado (há vagas por padrão).</summary>
    private static FaseCronograma FaseConforme() => FaseCronograma.Criar(
        ordem: 1,
        faseCanonicaOrigemId: new Guid("eeee0000-0000-4000-8000-000000000001"),
        codigo: "RESULTADO_FINAL",
        donoInstitucional: "CEPS",
        origemData: OrigemDataFase.Propria,
        agrupaEtapas: true,
        permiteComplementacao: false,
        produzResultado: true,
        resultadoDefinitivo: true,
        coletaInscricao: false,
        inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
        atoProduzidoCodigo: "RESULTADO_FINAL",
        atoProduzidoEfeitoIrreversivel: false,
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
            regraOrdemAlocacao: Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, 'd'),
            nOpcoesAlocacao: 1,
            regrasEliminacao: eliminacoes).Value!;

    private static DadosEdital Dados() => DadosEdital.Criar(
        "001/2026",
        new DateOnly(2026, 1, 1),
        new DateOnly(2026, 1, 31),
        new Guid("dddd0000-0000-4000-8000-000000000001")).Value!;

    /// <summary>
    /// Snapshot das <b>seis dimensões</b> mais o status — é sobre ele que o CA-07 asserta.
    /// Comparar só o <c>Result</c> deixaria passar exatamente a implementação que o CA-07
    /// existe para proibir: a que aplica e depois falha.
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
