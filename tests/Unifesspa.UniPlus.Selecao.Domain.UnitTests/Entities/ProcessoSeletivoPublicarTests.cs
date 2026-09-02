namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura de <see cref="ProcessoSeletivo.Publicar"/> (RN08, Story #759 T4
/// #785) — gate de conformidade, transição atômica, congelamento da
/// <see cref="VersaoConfiguracao"/> e bloqueio de mutação pós-publicação
/// (CA-04). Mapa de testes de #759: <c>Lifecycle_TransicaoInvalidaRecusada</c>,
/// <c>Publicacao_RecusaSemParametrosObrigatorios</c>,
/// <c>Publicacao_AtomicaStatusESnapshot</c>, <c>PosPublicacao_MutacaoBloqueada_422</c>.
/// </summary>
public sealed class ProcessoSeletivoPublicarTests
{
    private static readonly string HashFixo = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];
    private static readonly byte[] BytesCanonicos = Encoding.UTF8.GetBytes(new JsonObject { ["status"] = "ok" }.ToJsonString());

    private static DadosEdital NovosDados() => DadosEdital.Criar(
        numero: "001/2026",
        periodoInscricaoInicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
        periodoInscricaoFim: new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)),
        documentoEditalId: Guid.CreateVersion7()).Value!;

    /// <summary>
    /// Monta um processo minimamente conforme (CA-07: etapas, oferta de
    /// atendimento, distribuição de vagas, classificação) — o par exigido
    /// pelo gate de conformidade de <see cref="ProcessoSeletivo.Publicar"/>.
    /// </summary>
    private static ProcessoSeletivo NovoProcessoConforme(
        OrigemCandidatos origemCandidatos = OrigemCandidatos.InscricaoPropria)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, origemCandidatos, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

        processo.DefinirEtapas([
            EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva").Value!, peso: 1m, ordem: 1).Value!,
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ReferenciaRegra regraDistribuicao = ReferenciaRegra.Criar(
            RegraDistribuicaoVagasCodigo.Institucional, "v1", HashFixo).Value!;
        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            modalidadeOrigemId: Guid.CreateVersion7(),
            codigo: "AC",
            descricao: null,
            naturezaLegal: NaturezaLegalModalidade.Ampla,
            composicaoVagas: ComposicaoVagasModalidade.ResidualDoVo,
            composicaoOrigemCodigo: null,
            regraRemanejamento: RegraRemanejamentoModalidade.Nenhuma,
            remanejamentoDestino: null,
            remanejamentoPar: null,
            remanejamentoFallback: null,
            criteriosCumulativos: [],
            acaoQuandoIndeferido: null,
            baseLegal: "Res. Unifesspa 532/2021",
            quantidadeDeclarada: 40).Value!;
        ConfiguracaoDistribuicaoVagas distribuicao = ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: Guid.CreateVersion7(),
            voBase: 40,
            pr: 1m,
            regraDistribuicao: regraDistribuicao,
            regraAjuste: null,
            referenciaDemografica: null,
            modalidades: [modalidade]).Value!;
        processo.DefinirDistribuicaoVagas([distribuicao], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ReferenciaRegra regraCalculo = ReferenciaRegra.Criar(
            RegraCalculoCodigo.ClassificacaoImportada, "v1", HashFixo).Value!;
        ReferenciaRegra regraOrdemAlocacao = ReferenciaRegra.Criar(
            RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", HashFixo).Value!;
        ConfiguracaoClassificacao classificacao = ConfiguracaoClassificacao.Criar(
            regraCalculo: regraCalculo,
            regraArredondamento: null,
            casasArredondamento: null,
            regraOrdemAlocacao: regraOrdemAlocacao,
            nOpcoesAlocacao: 1,
            regrasEliminacao: [], baseadoEmEnem: false).Value!;
        processo.DefinirClassificacao(classificacao, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirCronogramaFases([FaseConforme()], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        // Issue #1112: publicar sem declarar cobrança de taxa é recusado (CA-01).
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        return processo;
    }

    /// <summary>Fase mínima e conforme: agrupa etapas (há 1 acima), produz resultado e coleta inscrição (há vagas e a origem é InscricaoPropria).</summary>
    private static FaseCronograma FaseConforme() => FaseCronograma.Criar(
        ordem: 1,
        faseCanonicaOrigemId: Guid.CreateVersion7(),
        codigo: "RESULTADO_FINAL",
        donoInstitucional: "CEPS",
        origemData: OrigemDataFase.Propria,
        agrupaEtapas: true,
        permiteComplementacao: false,
        produzResultado: true,
        resultadoDefinitivo: true,
        coletaInscricao: true,
        inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
        atoProduzidoCodigo: "RESULTADO_FINAL",
        atoProduzidoEfeitoIrreversivel: false,
        bancasRequeridas: [],
        regraRecurso: null).Value!;

    [Fact(DisplayName = "Publicacao_AtomicaStatusESnapshot — processo conforme publica, congela a versão 1 e transita status")]
    public void Publicar_ProcessoConforme_TransitaStatusECongelaVersao()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        DadosEdital dados = NovosDados();

        Result<VersaoConfiguracao> resultado = processo.Publicar(
            dados, BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123", TimeProvider.System, ContextoDeContagemDePrazos.SemCalendario);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.Status.Should().Be(StatusProcesso.Publicado);

        VersaoConfiguracao versao = resultado.Value!;
        versao.NumeroVersao.Should().Be(1, "a abertura abre a cadeia de configuração");
        versao.ProcessoSeletivoId.Should().Be(processo.Id);
        versao.AtoCriadorHash.Should().Be(HashFixo, "o hash do documento publicado é a metade-hash da referência por valor ao ato");
        versao.AtoCriadorId.Should().NotBeEmpty("a raiz decide o id do ato — a versão precisa referenciá-lo antes de o ato existir (ADR-0108)");
        versao.AtoCriadorRetificaId.Should().BeNull("a abertura não emenda ato algum: retificar é uma relação, e aqui não há a quem se relacionar");
    }

    [Fact(DisplayName = "Publicar — o id do ato é um Guid v7 ancorado no MESMO instante da vigência da versão que ele cria")]
    public void Publicar_IdDoAto_AncoradoNoInstanteDaVersao()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        DateTimeOffset instante = new(2026, 3, 13, 19, 0, 0, TimeSpan.Zero);

        Result<VersaoConfiguracao> resultado = processo.Publicar(
            NovosDados(), BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123", new RelogioFixo(instante), ContextoDeContagemDePrazos.SemCalendario);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        VersaoConfiguracao versao = resultado.Value!;

        versao.VigenteAPartirDe.Should().Be(instante);
        versao.AtoCriadorId.Version.Should().Be(7, "Guid v7 — o id carrega o instante, e é ordenável por ele (ADR-0032)");

        // O id do ato e a vigência da versão descrevem o mesmo fato: se cada um viesse de
        // uma leitura de relógio própria, discordariam. O timestamp embutido no v7 tem
        // resolução de milissegundo — é a granularidade em que a igualdade é verificável.
        DateTimeOffset instanteNoId = InstanteDoGuidV7(versao.AtoCriadorId);
        instanteNoId.Should().Be(
            new DateTimeOffset(instante.UtcDateTime.AddTicks(-(instante.UtcDateTime.Ticks % TimeSpan.TicksPerMillisecond)), TimeSpan.Zero),
            "uma leitura do relógio por operação atômica — o ato e a versão que ele cria nascem do mesmo instante");
    }

    [Fact(DisplayName = "Publicacao_AtomicaStatusESnapshot — evento carrega os identificadores forenses completos")]
    public void Publicar_ProcessoConforme_EmiteEventoComIdentificadoresCompletos()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        DadosEdital dados = NovosDados();

        Result<VersaoConfiguracao> resultado = processo.Publicar(
            dados, BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123", TimeProvider.System, ContextoDeContagemDePrazos.SemCalendario);

        resultado.IsSuccess.Should().BeTrue();
        Domain.Events.ProcessoPublicadoEvent evento = processo.DomainEvents
            .OfType<Domain.Events.ProcessoPublicadoEvent>().Should().ContainSingle().Subject;

        evento.ProcessoSeletivoId.Should().Be(processo.Id);
        // EditalId é o nome histórico do membro — contrato do envelope durável e do schema
        // Avro (ver ProcessoPublicadoEvent). O VALOR sempre foi o do ato criador, e continua.
        evento.EditalId.Should().Be(resultado.Value!.AtoCriadorId);
        evento.SnapshotPublicacaoId.Should().Be(resultado.Value!.Id);
        evento.HashConfiguracao.Should().Be(resultado.Value!.HashConfiguracao);
        evento.HashEdital.Should().Be(HashFixo);
        evento.OccurredOn.Should().Be(
            resultado.Value!.VigenteAPartirDe,
            "o fato ocorreu no instante do SISTEMA — o mesmo que ordena as versões (ADR-0104)");
    }

    [Fact(DisplayName = "Publicacao_RecusaSemParametrosObrigatorios — processo sem etapas recusa com checklist de pendências (CA-03)")]
    public void Publicar_SemEtapas_RecusaComConformidadeInsuficiente()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS incompleto", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        // Nenhuma dimensão obrigatória definida — Etapas/Atendimento/Distribuição/Classificação ausentes.

        Result<VersaoConfiguracao> resultado = processo.Publicar(
            NovosDados(), BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123", TimeProvider.System, ContextoDeContagemDePrazos.SemCalendario);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.ConformidadeInsuficiente");
        processo.Status.Should().Be(StatusProcesso.Rascunho, "publicação recusada não transita o status");
        processo.DequeueDomainEvents().Should().BeEmpty("nada é enfileirado numa publicação recusada");
    }

    [Fact(DisplayName = "Lifecycle_TransicaoInvalidaRecusada — publicar processo já publicado é recusado (CA-09)")]
    public void Publicar_ProcessoJaPublicado_RecusaTransicaoInvalida()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        processo.Publicar(NovosDados(), BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123", TimeProvider.System, ContextoDeContagemDePrazos.SemCalendario)
            .IsSuccess.Should().BeTrue();
        processo.DequeueDomainEvents();

        Result<VersaoConfiguracao> segundaTentativa = processo.Publicar(
            NovosDados(), BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123", TimeProvider.System, ContextoDeContagemDePrazos.SemCalendario);

        segundaTentativa.IsFailure.Should().BeTrue();
        segundaTentativa.Error!.Code.Should().Be("ProcessoSeletivo.TransicaoInvalida");
        processo.DequeueDomainEvents().Should().BeEmpty("a segunda tentativa não emite ato nem versão");
    }

    [Theory(DisplayName = "PosPublicacao_MutacaoBloqueada_422 — todo Definir* recusa mutação após publicação (CA-04)")]
    [InlineData("etapas")]
    [InlineData("ofertaAtendimento")]
    [InlineData("distribuicaoVagas")]
    [InlineData("bonusRegional")]
    [InlineData("criteriosDesempate")]
    [InlineData("classificacao")]
    [InlineData("cronogramaFases")]
    [InlineData("documentosExigidos")]
    public void DefinirX_ProcessoPublicado_RecusaMutacao(string dimensao)
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        processo.Publicar(NovosDados(), BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123", TimeProvider.System, ContextoDeContagemDePrazos.SemCalendario)
            .IsSuccess.Should().BeTrue();

        Result resultado = dimensao switch
        {
            "etapas" => processo.DefinirEtapas([EtapaProcesso.Criar("Nova Etapa", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva").Value!, peso: 1m, ordem: 1).Value!], PrecondicaoIfMatch.Ausente),
            "ofertaAtendimento" => processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente),
            "distribuicaoVagas" => processo.DefinirDistribuicaoVagas([], PrecondicaoIfMatch.Ausente),
            "bonusRegional" => processo.DefinirBonusRegional(null, PrecondicaoIfMatch.Ausente),
            "criteriosDesempate" => processo.DefinirCriteriosDesempate([], PrecondicaoIfMatch.Ausente),
            "classificacao" => processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
                ReferenciaRegra.Criar(RegraCalculoCodigo.ClassificacaoImportada, "v1", HashFixo).Value!,
                null, null,
                ReferenciaRegra.Criar(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", HashFixo).Value!,
                1, [], baseadoEmEnem: false).Value!, PrecondicaoIfMatch.Ausente),
            "cronogramaFases" => processo.DefinirCronogramaFases([FaseConforme()], [], PrecondicaoIfMatch.Ausente),
            "documentosExigidos" => processo.DefinirDocumentosExigidos([], PrecondicaoIfMatch.Ausente),
            _ => throw new InvalidOperationException("Dimensão desconhecida."),
        };

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
    }

    // ── Story #554 (PR #895) — guarda fail-closed (B-01) e CA-01 ──

    /// <summary>
    /// Base legal RESOLVIDO qualquer (Story #554, PR #898) — as fixtures abaixo testam
    /// checagens POSTERIORES ao 5º item de <see cref="ProcessoSeletivo.AvaliarConformidade"/>
    /// (B-01/CA-01), então precisam satisfazer o gate de base legal primeiro, ou nunca o
    /// alcançariam.
    /// </summary>
    private static DocumentoExigidoBaseLegal BaseLegalResolvidaQualquer() => DocumentoExigidoBaseLegal.Criar(
        "Lei 12.711/2012, art. 3º", TipoAbrangencia.InternaEdital, StatusBaseLegal.Resolvido, null).Value!;

    private static DocumentoExigido ExigenciaCondicionalVaziaObrigatoria(Guid exigidoNaFaseId) => DocumentoExigido.Criar(
        exigidoNaFaseId,
        tipoDocumentoOrigemId: Guid.CreateVersion7(),
        tipoDocumentoCodigo: "CERTIDAO_RESERVISTA",
        tipoDocumentoNome: "Certidão de reservista",
        tipoDocumentoCategoria: "MILITAR",
        aplicabilidade: Aplicabilidade.Condicional,
        obrigatorio: true,
        consequenciaIndeferimento: null,
        condicoes: [], basesLegais: [BaseLegalResolvidaQualquer()], idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;

    private static DocumentoExigido ExigenciaGeral(Guid exigidoNaFaseId) => DocumentoExigido.Criar(
        exigidoNaFaseId,
        tipoDocumentoOrigemId: Guid.CreateVersion7(),
        tipoDocumentoCodigo: "IDENTIDADE",
        tipoDocumentoNome: "Documento de identidade",
        tipoDocumentoCategoria: "PESSOAL",
        aplicabilidade: Aplicabilidade.Geral,
        obrigatorio: true,
        consequenciaIndeferimento: null,
        condicoes: [], basesLegais: [BaseLegalResolvidaQualquer()], idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;

    [Fact(DisplayName = "CA-01: publicar com exigência CONDICIONAL vazia obrigatória é bloqueado")]
    public void Publicar_CondicionalVaziaObrigatoria_Bloqueia()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(ExigenciaCondicionalVaziaObrigatoria(faseId), 0).Value!], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        Result<VersaoConfiguracao> resultado = processo.Publicar(
            NovosDados(), BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123", TimeProvider.System, ContextoDeContagemDePrazos.SemCalendario);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoExigido.CondicionalVaziaDeterminaResultado");
    }

    [Fact(DisplayName = "Story #554/PR #903: publicar com exigência GERAL configurada é aceito — a guarda B-01 foi removida (issue #548)")]
    public void Publicar_ExigenciaGeralConfigurada_Aceita()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(ExigenciaGeral(faseId), 0).Value!], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        Result<VersaoConfiguracao> resultado = processo.Publicar(
            NovosDados(), BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123", TimeProvider.System, ContextoDeContagemDePrazos.SemCalendario);

        resultado.IsSuccess.Should().BeTrue(
            resultado.Error?.Message ?? "o bloco documentosExigidos.exigencias deixou de ser stub — nada mais bloqueia esta publicação");
    }

    [Fact(DisplayName = "Publicar sem nenhum documento exigido configurado não é afetado pela guarda (contraprova)")]
    public void Publicar_SemDocumentosExigidos_NaoBloqueiaPorExigencias()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();

        Result<VersaoConfiguracao> resultado = processo.Publicar(
            NovosDados(), BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123", TimeProvider.System, ContextoDeContagemDePrazos.SemCalendario);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    // ── Story #554 (PR #896, issue #892) — B-03: referência temporal de fatos ──

    private static DocumentoExigido ExigenciaCondicionalComGatilhoPorFaixaEtaria(Guid exigidoNaFaseId)
    {
        CondicaoGatilho condicao = CondicaoGatilho.Criar(
            0, "FAIXA_ETARIA", Operador.MaiorIgual, JsonSerializer.SerializeToElement(18)).Value!;
        return DocumentoExigido.Criar(
            exigidoNaFaseId,
            tipoDocumentoOrigemId: Guid.CreateVersion7(),
            tipoDocumentoCodigo: "DECLARACAO_MAIORIDADE",
            tipoDocumentoNome: "Declaração de maioridade",
            tipoDocumentoCategoria: "PESSOAL",
            aplicabilidade: Aplicabilidade.Condicional,
            obrigatorio: true,
            consequenciaIndeferimento: null,
            condicoes: [condicao], basesLegais: [BaseLegalResolvidaQualquer()], idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;
    }

    [Fact(DisplayName = "DefinirReferenciaTemporalFatos: fase de outro processo é recusada")]
    public void DefinirReferenciaTemporalFatos_FaseDeOutroProcesso_Recusa()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        ReferenciaTemporalFatos referencia = ReferenciaTemporalFatos.Criar(
            ReferenciaTipo.FimFase, null, Guid.CreateVersion7()).Value!;

        Result resultado = processo.DefinirReferenciaTemporalFatos(referencia, PrecondicaoIfMatch.Curinga);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ReferenciaTemporalFatos.FaseNaoPertenceAoProcesso");
    }

    [Fact(DisplayName = "DefinirReferenciaTemporalFatos: fase do próprio cronograma é aceita (contraprova)")]
    public void DefinirReferenciaTemporalFatos_FaseDoProprioProcesso_Aceita()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        Guid faseId = processo.CronogramaFases.Single().Id;
        ReferenciaTemporalFatos referencia = ReferenciaTemporalFatos.Criar(ReferenciaTipo.FimFase, null, faseId).Value!;

        Result resultado = processo.DefinirReferenciaTemporalFatos(referencia, PrecondicaoIfMatch.Curinga);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.ReferenciaTemporalFatos.Should().Be(referencia);
    }

    [Fact(DisplayName = "DefinirReferenciaTemporalFatos: redefinir para null é aceito — presença é 0..1 (contraprova)")]
    public void DefinirReferenciaTemporalFatos_Nulo_Aceita()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirReferenciaTemporalFatos(
            ReferenciaTemporalFatos.Criar(ReferenciaTipo.FimFase, null, faseId).Value!, PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirReferenciaTemporalFatos(null, PrecondicaoIfMatch.Curinga);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.ReferenciaTemporalFatos.Should().BeNull();
    }

    [Fact(DisplayName = "B-03: gatilho por FAIXA_ETARIA sem referência configurada é bloqueado — a guarda B-01 (issue #547) que antes mascarava esta checagem foi removida na PR #903 (issue #548)")]
    public void Publicar_GatilhoPorFaixaEtariaSemReferencia_BloqueadoPorB03()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(ExigenciaCondicionalComGatilhoPorFaixaEtaria(faseId), 0).Value!], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        // Nenhuma ReferenciaTemporalFatos configurada — com a guarda B-01 removida, a
        // checagem B-03 (issue #892) agora é alcançada e recusa nomeadamente.

        Result<VersaoConfiguracao> resultado = processo.Publicar(
            NovosDados(), BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123", TimeProvider.System, ContextoDeContagemDePrazos.SemCalendario);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.ReferenciaTemporalFatosAusente");
    }

    [Fact(DisplayName = "Publicar sem nenhum gatilho por FAIXA_ETARIA não é afetado pela pendência B-03 (contraprova)")]
    public void Publicar_SemGatilhoPorFaixaEtaria_NaoBloqueiaPorReferenciaTemporalFatos()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirReferenciaTemporalFatos(
            ReferenciaTemporalFatos.Criar(ReferenciaTipo.FimFase, null, faseId).Value!, PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        Result<VersaoConfiguracao> resultado = processo.Publicar(
            NovosDados(), BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123", TimeProvider.System, ContextoDeContagemDePrazos.SemCalendario);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    /// <summary>Acrescenta ao cronograma conforme uma segunda fase DELEGADA (sem janela) — a única forma de ter uma fase sem Fim, já que PROPRIA exige janela (CA-07).</summary>
    private static Guid AcrescentarFaseSemExtremo(ProcessoSeletivo processo)
    {
        FaseCronograma semExtremo = FaseCronograma.Criar(
            ordem: 2,
            faseCanonicaOrigemId: Guid.CreateVersion7(),
            codigo: "HOMOLOGACAO",
            donoInstitucional: "CEPS",
            origemData: OrigemDataFase.Delegada,
            agrupaEtapas: false,
            permiteComplementacao: false,
            produzResultado: false,
            resultadoDefinitivo: false,
            coletaInscricao: false,
            inicio: null,
            fim: null,
            atoProduzidoCodigo: null,
            atoProduzidoEfeitoIrreversivel: false,
            bancasRequeridas: [],
            regraRecurso: null).Value!;
        processo.DefinirCronogramaFases([FaseConforme(), semExtremo], [], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        return semExtremo.Id;
    }

    [Fact(DisplayName = "B-03: FIM_FASE apontando para fase sem Fim definido bloqueia publicação")]
    public void Publicar_FimFaseSemExtremoDefinido_BloqueadoPorB03()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        Guid faseSemExtremoId = AcrescentarFaseSemExtremo(processo);
        Guid faseComEtapaId = processo.CronogramaFases.Single(f => f.Ordem == 1).Id;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(ExigenciaCondicionalComGatilhoPorFaixaEtaria(faseComEtapaId), 0).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();
        processo.DefinirReferenciaTemporalFatos(
            ReferenciaTemporalFatos.Criar(ReferenciaTipo.FimFase, null, faseSemExtremoId).Value!, PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        Result<VersaoConfiguracao> resultado = processo.Publicar(
            NovosDados(), BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123", TimeProvider.System, ContextoDeContagemDePrazos.SemCalendario);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.ReferenciaTemporalFatosExtremoAusente");
    }

    /// <summary>
    /// Cronograma SEM nenhuma fase que colete inscrição, com o resultado numa fase PROPRIA.
    /// </summary>
    /// <remarks>
    /// Era, antes da issue #1350, um cronograma com a coleta numa fase DELEGADA sem janela — "a
    /// única forma de ter uma fase ColetaInscricao=true sem Fim". Aquele estado deixou de ser
    /// publicável: a fase que coleta inscrição passou a exigir janela, e a recusa dela roda no
    /// gate do cronograma, ANTES do B-03. O teste continuaria verde recusando pelo motivo errado.
    /// <para>
    /// O caminho que sobrou para alcançar o B-03 é este: nenhuma fase de coleta, com FIM_INSCRICAO
    /// declarado assim mesmo — <c>DefinirReferenciaTemporalFatos</c> não guarda contra isso, e é
    /// por isso que o gate segue sendo a única barreira. Sem ele,
    /// <c>ResolverDataReferenciaFatos</c> lança e o 422 vira 500.
    /// </para>
    /// </remarks>
    private static Guid DefinirCronogramaSemFaseDeColeta(ProcessoSeletivo processo)
    {
        FaseCronograma resultado = FaseCronograma.Criar(
            ordem: 2,
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
        processo.DefinirCronogramaFases([resultado], [], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        return resultado.Id;
    }

    /// <summary>
    /// O único caminho que resta até o B-03 depois da issue #1350, e o motivo de ele continuar
    /// existindo.
    /// </summary>
    /// <remarks>
    /// Os outros dois caminhos foram fechados por gates que rodam antes: com inscrição própria,
    /// a ausência de fase de coleta é recusada por <c>InscricaoPropriaSemFaseDeColeta</c>; com
    /// fase de coleta presente, a falta de janela é recusada por
    /// <c>FaseQueColetaInscricaoSemJanela</c>. Sobra a importação externa, que legitimamente não
    /// tem fase de coleta e ainda assim aceita declarar FIM_INSCRICAO —
    /// <c>DefinirReferenciaTemporalFatos</c> não guarda contra isso.
    /// </remarks>
    [Fact(DisplayName = "B-03: FIM_INSCRICAO sem nenhuma fase que colete inscrição bloqueia publicação")]
    public void Publicar_FimInscricaoSemFaseComFimDefinido_BloqueadoPorB03()
    {
        ProcessoSeletivo processo = NovoProcessoConforme(OrigemCandidatos.ImportacaoExterna);
        Guid faseColetaId = DefinirCronogramaSemFaseDeColeta(processo);
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(ExigenciaCondicionalComGatilhoPorFaixaEtaria(faseColetaId), 0).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();
        processo.DefinirReferenciaTemporalFatos(
            ReferenciaTemporalFatos.Criar(ReferenciaTipo.FimInscricao, null, null).Value!, PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        Result<VersaoConfiguracao> resultado = processo.Publicar(
            NovosDados(), BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123", TimeProvider.System, ContextoDeContagemDePrazos.SemCalendario);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.ReferenciaTemporalFatosFimInscricaoIndisponivel");
    }

    [Fact(DisplayName = "B-03: FIM_INSCRICAO com mais de uma fase de coleta resolve pela menor Ordem, não pela ordem de inserção — achado de revisão da PR #903")]
    public void ResolverDataReferenciaFatos_FimInscricaoComDuasFasesDeColeta_ResolvePelaMenorOrdem()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();

        FaseCronograma coletaOrdem1 = FaseCronograma.Criar(
            ordem: 1,
            faseCanonicaOrigemId: Guid.CreateVersion7(),
            codigo: "INSCRICAO_AC",
            donoInstitucional: "CEPS",
            origemData: OrigemDataFase.Propria,
            agrupaEtapas: false,
            permiteComplementacao: false,
            produzResultado: false,
            resultadoDefinitivo: false,
            coletaInscricao: true,
            inicio: new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero),
            atoProduzidoCodigo: null,
            atoProduzidoEfeitoIrreversivel: false,
            bancasRequeridas: [],
            regraRecurso: null).Value!;
        FaseCronograma coletaOrdem2 = FaseCronograma.Criar(
            ordem: 2,
            faseCanonicaOrigemId: Guid.CreateVersion7(),
            codigo: "INSCRICAO_REMANEJAMENTO",
            donoInstitucional: "CEPS",
            origemData: OrigemDataFase.Propria,
            agrupaEtapas: true,
            permiteComplementacao: false,
            produzResultado: true,
            resultadoDefinitivo: true,
            coletaInscricao: true,
            inicio: new DateTimeOffset(2026, 1, 20, 12, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 1, 25, 12, 0, 0, TimeSpan.Zero),
            atoProduzidoCodigo: "RESULTADO_FINAL",
            atoProduzidoEfeitoIrreversivel: false,
            bancasRequeridas: [],
            regraRecurso: null).Value!;

        // Insere a fase de Ordem 2 ANTES da de Ordem 1 — _cronogramaFases preserva a ordem
        // de ENTRADA de DefinirCronogramaFases, diferente do envelope (sempre ordenado por
        // Ordem). Se a resolução dependesse dessa ordem de inserção, pegaria a fase errada.
        processo.DefinirCronogramaFases([coletaOrdem2, coletaOrdem1], [], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        Guid faseOrdem1Id = processo.CronogramaFases.Single(f => f.Ordem == 1).Id;

        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(ExigenciaCondicionalComGatilhoPorFaixaEtaria(faseOrdem1Id), 0).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();
        processo.DefinirReferenciaTemporalFatos(
            ReferenciaTemporalFatos.Criar(ReferenciaTipo.FimInscricao, null, null).Value!, PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        processo.ResolverDataReferenciaFatos(TimeZoneInfo.FindSystemTimeZoneById(FusoInstitucional.ZoneId)).Should().Be(new DateOnly(2026, 1, 15),
            "a fase de Ordem 1 (Fim 2026-01-15) precisa vencer a de Ordem 2 (Fim 2026-01-25) por Ordem — se a " +
            "escolha dependesse da ordem de inserção em _cronogramaFases, a reidratação (sempre ordenada por " +
            "Ordem) poderia resolver outra data e quebrar o round-trip");
    }

    [Fact(DisplayName = "B-03: ResolverDataReferenciaFatos converte para o fuso institucional — um Fim de madrugada em UTC cai no dia anterior local")]
    public void ResolverDataReferenciaFatos_FimDeMadrugadaEmUtc_ResolveODiaAnteriorNoFusoInstitucional()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();

        // 2026-03-02T01:30:00Z, subtraídas 3h de fuso (America/Belem, sem horário de
        // verão), cai em 2026-03-01T22:30 local — dia DIFERENTE do dia UTC. Uma resolução
        // que apenas truncasse a data (sem converter o fuso) devolveria 02/03, um dia
        // inteiro adiantado em relação ao que o administrador viu na tela.
        FaseCronograma faseComVirada = FaseCronograma.Criar(
            ordem: 2,
            faseCanonicaOrigemId: Guid.CreateVersion7(),
            codigo: "RESULTADO_PRELIMINAR",
            donoInstitucional: "CEPS",
            origemData: OrigemDataFase.Propria,
            agrupaEtapas: false,
            permiteComplementacao: false,
            produzResultado: false,
            resultadoDefinitivo: false,
            coletaInscricao: false,
            inicio: new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 3, 2, 1, 30, 0, TimeSpan.Zero),
            atoProduzidoCodigo: null,
            atoProduzidoEfeitoIrreversivel: false,
            bancasRequeridas: [],
            regraRecurso: null).Value!;
        // A segunda fase precisa entrar ANTES de configurar a exigência: uma vez que a
        // exigência referencia a fase 1 (ExigidoNaFaseId), redefinir o cronograma depois
        // recriaria a fase 1 com outra FaseCanonicaOrigemId e o guard
        // FaseCronograma.ReferenciadaPorExigenciaViva recusaria a redefinição.
        processo.DefinirCronogramaFases([FaseConforme(), faseComVirada], [], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        Guid faseComEtapaId = processo.CronogramaFases.Single(f => f.Ordem == 1).Id;
        Guid faseComViradaId = processo.CronogramaFases.Single(f => f.Ordem == 2).Id;

        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(ExigenciaCondicionalComGatilhoPorFaixaEtaria(faseComEtapaId), 0).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();
        processo.DefinirReferenciaTemporalFatos(
            ReferenciaTemporalFatos.Criar(ReferenciaTipo.FimFase, null, faseComViradaId).Value!, PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        processo.ResolverDataReferenciaFatos(TimeZoneInfo.FindSystemTimeZoneById(FusoInstitucional.ZoneId)).Should().Be(new DateOnly(2026, 3, 1),
            "o Fim (2026-03-02T01:30:00Z) convertido para o fuso institucional (America/Belem, UTC-3, sem DST) é 2026-03-01T22:30 — o dia LOCAL, não o dia UTC");
    }

    // ── Story #554 (PR #903, issue #548) — CA-05: coerência consequência↔ação da vaga ──

    private static ModalidadeSelecionada NovaModalidadeComAcao(
        string codigo, NaturezaLegalModalidade naturezaLegal, ComposicaoVagasModalidade composicaoVagas, string? acaoQuandoIndeferido) =>
        ModalidadeSelecionada.Criar(
            modalidadeOrigemId: Guid.CreateVersion7(),
            codigo: codigo,
            descricao: null,
            naturezaLegal: naturezaLegal,
            composicaoVagas: composicaoVagas,
            composicaoOrigemCodigo: null,
            regraRemanejamento: naturezaLegal == NaturezaLegalModalidade.CotaReservada
                ? RegraRemanejamentoModalidade.SegueCascata
                : RegraRemanejamentoModalidade.Nenhuma,
            remanejamentoDestino: null,
            remanejamentoPar: null,
            remanejamentoFallback: null,
            criteriosCumulativos: [],
            acaoQuandoIndeferido: acaoQuandoIndeferido,
            baseLegal: "Lei 12.711/2012",
            // Story #575: sob o regime federal (Lei 12.711), a quantidade de uma modalidade
            // DentroDoVr/ResidualDoVo é CALCULADA pela fórmula — informá-la é recusado
            // (ConfiguracaoDistribuicaoVagas.QuantidadeCalculadaNaoInformavel). Só natureza
            // Suplementar (regime institucional, quantidade fixada pelo edital) precisa do 10.
            quantidadeDeclarada: naturezaLegal == NaturezaLegalModalidade.Suplementar ? 10 : null).Value!;

    /// <summary>
    /// A matriz legal 8×7 completa, fallback AC (Story #575) — usada por
    /// <see cref="NovoProcessoComModalidade"/> sempre que a modalidade do chamador é
    /// <see cref="NaturezaLegalModalidade.CotaReservada"/>: desde a #575, RN-CASCATA-2b
    /// recusa <c>SegueCascata</c> fora do regime federal, então uma cota reservada só
    /// publica com uma cascata legítima por trás.
    /// </summary>
    private static ConfiguracaoCascataRemanejamento CascataLegalCompleta()
    {
        List<DestinoRemanejamento> destinos = [];
        foreach (string origem in ModalidadesFederaisLei12711.Codigos)
        {
            string[] ordemDestinos = [.. ModalidadesFederaisLei12711.Codigos.Where(o => o != origem)];
            for (int i = 0; i < ordemDestinos.Length; i++)
            {
                destinos.Add(DestinoRemanejamento.Criar(origem, i + 1, ordemDestinos[i]).Value!);
            }
        }

        ReferenciaRegra regraCascata = ReferenciaRegra.Criar(RegraRemanejamentoCodigo.Cascata, "v1", HashFixo).Value!;
        return ConfiguracaoCascataRemanejamento.Criar(regraCascata, ModalidadesFederaisLei12711.Ac, destinos).Value!;
    }

    /// <summary>
    /// Variante de <see cref="NovoProcessoConforme"/> com UMA modalidade escolhida pelo
    /// chamador (Story #554, PR #903, CA-05) — para exercitar a coerência consequência↔ação
    /// da vaga contra uma natureza/ação específica, não a "AC" fixa do helper padrão.
    /// </summary>
    /// <remarks>
    /// Quando a modalidade do chamador é <see cref="NaturezaLegalModalidade.CotaReservada"/>
    /// (sempre <c>SegueCascata</c>, invariante de <c>ModalidadeSelecionada.Criar</c>), a oferta
    /// precisa ser Lei 12.711 completa (as 8 federais + AC, demografia, regra de ajuste) com
    /// uma cascata legal por trás — RN-CASCATA-2b (Story #575) recusa <c>SegueCascata</c> fora
    /// do regime federal. As demais naturezas (ampla, suplementar) continuam no ramo
    /// institucional simples de antes, com a única modalidade do chamador.
    /// </remarks>
    private static ProcessoSeletivo NovoProcessoComModalidade(ModalidadeSelecionada modalidade)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

        processo.DefinirEtapas([
            EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva").Value!, peso: 1m, ordem: 1).Value!,
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ConfiguracaoDistribuicaoVagas distribuicao;
        if (modalidade.NaturezaLegal == NaturezaLegalModalidade.CotaReservada)
        {
            List<ModalidadeSelecionada> modalidadesFederais =
            [
                .. ModalidadesFederaisLei12711.Codigos
                    .Where(codigo => codigo != modalidade.Codigo)
                    .Select(codigo => ModalidadeSelecionada.Criar(
                        Guid.CreateVersion7(), codigo, null, NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDoVr,
                        null, RegraRemanejamentoModalidade.SegueCascata, null, null, null, [], null, "Lei 12.711/2012", quantidadeDeclarada: null).Value!),
                modalidade,
                ModalidadeSelecionada.Criar(
                    Guid.CreateVersion7(), ModalidadesFederaisLei12711.Ac, null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo,
                    null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, [], null, "base legal", quantidadeDeclarada: null).Value!,
            ];
            distribuicao = ConfiguracaoDistribuicaoVagas.Criar(
                ofertaCursoOrigemId: Guid.CreateVersion7(),
                voBase: 40,
                pr: 0.5m,
                regraDistribuicao: ReferenciaRegra.Criar(RegraDistribuicaoVagasCodigo.Lei12711, "v1", HashFixo).Value!,
                regraAjuste: ReferenciaRegra.Criar("RECONCILIACAO-VAGAS-ART11-PU", "v1", HashFixo).Value!,
                referenciaDemografica: ReferenciaReservaDemograficaSnapshot.Criar(Guid.CreateVersion7(), "2022", 79m, 1.5m, 8.5m, "Censo 2022").Value!,
                modalidades: modalidadesFederais).Value!;
            processo.DefinirDistribuicaoVagas([distribuicao], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
            processo.DefinirCascataRemanejamento(CascataLegalCompleta(), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        }
        else
        {
            distribuicao = ConfiguracaoDistribuicaoVagas.Criar(
                ofertaCursoOrigemId: Guid.CreateVersion7(),
                voBase: modalidade.QuantidadeDeclarada ?? 40,
                pr: 1m,
                regraDistribuicao: ReferenciaRegra.Criar(RegraDistribuicaoVagasCodigo.Institucional, "v1", HashFixo).Value!,
                regraAjuste: null,
                referenciaDemografica: null,
                modalidades: [modalidade]).Value!;
            processo.DefinirDistribuicaoVagas([distribuicao], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        }

        ConfiguracaoClassificacao classificacao = ConfiguracaoClassificacao.Criar(
            regraCalculo: ReferenciaRegra.Criar(RegraCalculoCodigo.ClassificacaoImportada, "v1", HashFixo).Value!,
            regraArredondamento: null,
            casasArredondamento: null,
            regraOrdemAlocacao: ReferenciaRegra.Criar(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", HashFixo).Value!,
            nOpcoesAlocacao: 1,
            regrasEliminacao: [], baseadoEmEnem: false).Value!;
        processo.DefinirClassificacao(classificacao, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirCronogramaFases([FaseConforme()], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        // Issue #1112: publicar sem declarar cobrança de taxa é recusado (CA-01).
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        return processo;
    }

    private static DocumentoExigido ExigenciaGeralComConsequencia(
        Guid exigidoNaFaseId, string tipoDocumentoCodigo, string tipoDocumentoNome, string tipoDocumentoCategoria, string consequenciaIndeferimento) =>
        DocumentoExigido.Criar(
            exigidoNaFaseId,
            tipoDocumentoOrigemId: Guid.CreateVersion7(),
            tipoDocumentoCodigo: tipoDocumentoCodigo,
            tipoDocumentoNome: tipoDocumentoNome,
            tipoDocumentoCategoria: tipoDocumentoCategoria,
            aplicabilidade: Aplicabilidade.Geral,
            obrigatorio: false,
            consequenciaIndeferimento: consequenciaIndeferimento,
            condicoes: [], basesLegais: [BaseLegalResolvidaQualquer()], idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;

    private static DocumentoExigido ExigenciaCondicionalPorModalidadeComConsequencia(
        Guid exigidoNaFaseId, string tipoDocumentoCodigo, string tipoDocumentoNome, string tipoDocumentoCategoria,
        string modalidadeCodigo, string consequenciaIndeferimento) =>
        DocumentoExigido.Criar(
            exigidoNaFaseId,
            tipoDocumentoOrigemId: Guid.CreateVersion7(),
            tipoDocumentoCodigo: tipoDocumentoCodigo,
            tipoDocumentoNome: tipoDocumentoNome,
            tipoDocumentoCategoria: tipoDocumentoCategoria,
            aplicabilidade: Aplicabilidade.Condicional,
            obrigatorio: false,
            consequenciaIndeferimento: consequenciaIndeferimento,
            condicoes: [CondicaoGatilho.Criar(0, "MODALIDADE", Operador.Igual, JsonSerializer.SerializeToElement(modalidadeCodigo)).Value!],
            basesLegais: [BaseLegalResolvidaQualquer()], idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;

    [Fact(DisplayName = "CA-05 (1/5 — heteroidentificação/indígena): ELIMINA é incoerente com RECLASSIFICA_AC da modalidade PPI")]
    public void Publicar_HeteroidentificacaoElimina_IncoerenteComAcaoDaModalidadePpi()
    {
        ModalidadeSelecionada ppi = NovaModalidadeComAcao(
            "LB_PPI", NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDoVr, "RECLASSIFICA_AC");
        ProcessoSeletivo processo = NovoProcessoComModalidade(ppi);
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos(
            [NoExigencia.CriarFolha(ExigenciaGeralComConsequencia(faseId, "RESULTADO_HETEROIDENTIFICACAO", "Resultado da banca de heteroidentificação", "HETEROIDENTIFICACAO", "ELIMINA"), 0).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result<VersaoConfiguracao> resultado = processo.Publicar(
            NovosDados(), BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123", TimeProvider.System, ContextoDeContagemDePrazos.SemCalendario);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoExigido.ConsequenciaIncoerenteComAcaoDaVaga");
    }

    [Fact(DisplayName = "CA-05 (2/5 — modalidade de cota): ELIMINA é incoerente com RECLASSIFICA_AC de uma cota reservada (comprovante de renda)")]
    public void Publicar_ComprovanteRendaElimina_IncoerenteComAcaoDaModalidadeDeCota()
    {
        ModalidadeSelecionada cota = NovaModalidadeComAcao(
            "LB_Q", NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDoVr, "RECLASSIFICA_AC");
        ProcessoSeletivo processo = NovoProcessoComModalidade(cota);
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos(
            [NoExigencia.CriarFolha(ExigenciaGeralComConsequencia(faseId, "COMPROVANTE_RENDA", "Comprovante de renda familiar", "RENDA", "ELIMINA"), 0).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result<VersaoConfiguracao> resultado = processo.Publicar(
            NovosDados(), BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123", TimeProvider.System, ContextoDeContagemDePrazos.SemCalendario);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoExigido.ConsequenciaIncoerenteComAcaoDaVaga");
    }

    [Fact(DisplayName = "CA-05 (3/5 — categoria incompatível): exigência CONDICIONAL só alcança a modalidade pelo gatilho, não pela categoria do documento")]
    public void Publicar_LaudoMedicoElimina_IncoerenteComAcaoDaModalidadeAlcancadaPorGatilho()
    {
        ModalidadeSelecionada suplementar = NovaModalidadeComAcao(
            "PSIQ_INDIGENA", NaturezaLegalModalidade.Suplementar, ComposicaoVagasModalidade.SuplementarAoTotal, "PENDENCIA_REENVIO");
        ProcessoSeletivo processo = NovoProcessoComModalidade(suplementar);
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos(
            [NoExigencia.CriarFolha(ExigenciaCondicionalPorModalidadeComConsequencia(faseId, "LAUDO_MEDICO", "Laudo médico", "SAUDE", "PSIQ_INDIGENA", "ELIMINA"), 0).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result<VersaoConfiguracao> resultado = processo.Publicar(
            NovosDados(), BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123", TimeProvider.System, ContextoDeContagemDePrazos.SemCalendario);

        resultado.IsFailure.Should().BeTrue(
            "a categoria do documento (SAUDE) não isenta a coerência — o que importa é a modalidade que o gatilho alcança");
        resultado.Error!.Code.Should().Be("DocumentoExigido.ConsequenciaIncoerenteComAcaoDaVaga");
    }

    [Fact(DisplayName = "CA-05 (4/5 — REMOVE_VANTAGEM sem vantagem viva): recusado quando o processo não tem bônus regional configurado")]
    public void Publicar_RemoveVantagemSemVantagemViva_Recusa()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos(
            [NoExigencia.CriarFolha(ExigenciaGeralComConsequencia(faseId, "COMPROVANTE_RESIDENCIA_CONVENIO", "Comprovante de residência no município do convênio", "BONUS_REGIONAL", "REMOVE_VANTAGEM"), 0).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        // Nenhum ConfiguracaoBonusRegional definido — RN05, toggle por presença: a
        // ausência da entidade já significa "sem vantagem viva" para remover.

        Result<VersaoConfiguracao> resultado = processo.Publicar(
            NovosDados(), BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123", TimeProvider.System, ContextoDeContagemDePrazos.SemCalendario);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoExigido.RemoveVantagemSemVantagemViva");
    }

    [Fact(DisplayName = "CA-05: REMOVE_VANTAGEM com bônus regional configurado é aceito (contraprova)")]
    public void Publicar_RemoveVantagemComVantagemViva_Aceita()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        processo.DefinirBonusRegional(
            ConfiguracaoBonusRegional.Criar(
                ReferenciaRegra.Criar(RegraBonusCodigo.Multiplicativo, "v1", HashFixo).Value!,
                fator: 1.2m, teto: null, municipioConvenio: "Marabá", baseLegal: "Res. Unifesspa 532/2021").Value!,
            PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos(
            [NoExigencia.CriarFolha(ExigenciaGeralComConsequencia(faseId, "COMPROVANTE_RESIDENCIA_CONVENIO", "Comprovante de residência no município do convênio", "BONUS_REGIONAL", "REMOVE_VANTAGEM"), 0).Value!],
            PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        Result<VersaoConfiguracao> resultado = processo.Publicar(
            NovosDados(), BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123", TimeProvider.System, ContextoDeContagemDePrazos.SemCalendario);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    private static DocumentoExigido ExigenciaCondicionalPorFaixaEtariaComConsequencia(
        Guid exigidoNaFaseId, string tipoDocumentoCodigo, string tipoDocumentoNome, string tipoDocumentoCategoria,
        string consequenciaIndeferimento) =>
        DocumentoExigido.Criar(
            exigidoNaFaseId,
            tipoDocumentoOrigemId: Guid.CreateVersion7(),
            tipoDocumentoCodigo: tipoDocumentoCodigo,
            tipoDocumentoNome: tipoDocumentoNome,
            tipoDocumentoCategoria: tipoDocumentoCategoria,
            aplicabilidade: Aplicabilidade.Condicional,
            obrigatorio: false,
            consequenciaIndeferimento: consequenciaIndeferimento,
            condicoes: [CondicaoGatilho.Criar(0, "FAIXA_ETARIA", Operador.MaiorIgual, JsonSerializer.SerializeToElement(18)).Value!],
            basesLegais: [BaseLegalResolvidaQualquer()], idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;

    [Fact(DisplayName = "CA-05 (5/5 — gatilho não-modal): ELIMINA condicionado só a FAIXA_ETARIA (nenhuma condição de MODALIDADE) ainda é incoerente com RECLASSIFICAR_AC — achado de revisão da PR #903")]
    public void Publicar_GatilhoPorFaixaEtariaSemCondicaoDeModalidadeElimina_IncoerenteComAcaoDaModalidade()
    {
        // O gatilho desta exigência é inteiramente sobre FAIXA_ETARIA — nenhuma condição
        // sobre MODALIDADE. Antes da correção, ModalidadesAlcancadasPor avaliava o gatilho
        // com um dicionário de fatos só com MODALIDADE; a condição de FAIXA_ETARIA (fato
        // ausente) avaliava como falsa (PredicadoDnf.Avaliar), reprovando a cláusula
        // inteira e escondendo QUALQUER modalidade do gate CA-05 — publicaria incoerente.
        ModalidadeSelecionada ppi = NovaModalidadeComAcao(
            "LB_PPI", NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDoVr, "RECLASSIFICAR_AC");
        ProcessoSeletivo processo = NovoProcessoComModalidade(ppi);
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos(
            [NoExigencia.CriarFolha(ExigenciaCondicionalPorFaixaEtariaComConsequencia(faseId, "DECLARACAO_MAIORIDADE", "Declaração de maioridade", "PESSOAL", "ELIMINA"), 0).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirReferenciaTemporalFatos(
            ReferenciaTemporalFatos.Criar(ReferenciaTipo.FimFase, null, faseId).Value!, PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        Result<VersaoConfiguracao> resultado = processo.Publicar(
            NovosDados(), BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123", TimeProvider.System, ContextoDeContagemDePrazos.SemCalendario);

        resultado.IsFailure.Should().BeTrue(
            "um gatilho sem NENHUMA condição de MODALIDADE não isenta a exigência do CA-05 — ela alcança " +
            "candidatos de qualquer modalidade, inclusive a PPI incoerente");
        resultado.Error!.Code.Should().Be("DocumentoExigido.ConsequenciaIncoerenteComAcaoDaVaga");
    }

    [Fact(DisplayName = "CA-05: modalidade sem ação de indeferimento declarada não é confrontada — a consequência da exigência decide sozinha")]
    public void Publicar_ModalidadeSemAcaoDeclarada_NaoDisparaCa05()
    {
        // A ausência de AcaoQuandoIndeferido é declaração, não omissão: a modalidade não
        // reclassifica, e quem responde pelo destino do candidato indeferido é a consequência
        // da exigência documental. Nenhuma das treze modalidades semeadas declara ação — se
        // null passasse a ser lido como RECLASSIFICAR_AC, toda exigência ELIMINA do catálogo
        // real deixaria de publicar. Este teste é o que segura essa leitura.
        ModalidadeSelecionada semAcao = NovaModalidadeComAcao(
            "LB_PPI", NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDoVr, acaoQuandoIndeferido: null);
        ProcessoSeletivo processo = NovoProcessoComModalidade(semAcao);
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos(
            [NoExigencia.CriarFolha(ExigenciaGeralComConsequencia(faseId, "RESULTADO_HETEROIDENTIFICACAO", "Resultado da banca de heteroidentificação", "HETEROIDENTIFICACAO", "ELIMINA"), 0).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result<VersaoConfiguracao> resultado = processo.Publicar(
            NovosDados(), BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123", TimeProvider.System, ContextoDeContagemDePrazos.SemCalendario);

        resultado.IsSuccess.Should().BeTrue(
            "ELIMINA seria incoerente com uma ação declarada de reclassificação, mas esta "
            + "modalidade não declara nenhuma — não há o que confrontar");
    }

    [Fact(DisplayName = "CA-05: RECLASSIFICA_AC (vocabulário de DocumentoExigido) é coerente com RECLASSIFICAR_AC (vocabulário real de ModalidadeSelecionada.AcaoQuandoIndeferido) — achado de revisão da PR #903")]
    public void Publicar_ReclassificaAcComAcaoRealReclassificarAc_Aceita()
    {
        // DocumentoExigido.ConsequenciaIndeferimento aceita "RECLASSIFICA_AC" (rol fechado
        // desde a PR #895), mas ModalidadeSelecionada.AcaoQuandoIndeferido — snapshot-copy do
        // cadastro real de Modalidade — só aceita "RECLASSIFICAR_AC"/"RECLASSIFICAR_REGRA_EDITAL"
        // (ck_modalidade_acao_quando_indeferido, módulo Configuração). Comparar os tokens
        // crus sempre reprovaria o único caso de reclassificação coerente que existe.
        ModalidadeSelecionada ppi = NovaModalidadeComAcao(
            "LB_PPI", NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDoVr, "RECLASSIFICAR_AC");
        ProcessoSeletivo processo = NovoProcessoComModalidade(ppi);
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos(
            [NoExigencia.CriarFolha(ExigenciaGeralComConsequencia(faseId, "RESULTADO_HETEROIDENTIFICACAO", "Resultado da banca de heteroidentificação", "HETEROIDENTIFICACAO", "RECLASSIFICA_AC"), 0).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result<VersaoConfiguracao> resultado = processo.Publicar(
            NovosDados(), BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123", TimeProvider.System, ContextoDeContagemDePrazos.SemCalendario);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    // ── Story #554/issue #549 (PR #898) — base legal 1:N, 5º item de AvaliarConformidade ──

    private static DocumentoExigidoBaseLegal BaseLegalDe(StatusBaseLegal status) => DocumentoExigidoBaseLegal.Criar(
        "Lei 12.711/2012, art. 3º", TipoAbrangencia.Federal, status, null).Value!;

    private static DocumentoExigido ExigenciaObrigatoriaCom(Guid exigidoNaFaseId, params DocumentoExigidoBaseLegal[] basesLegais) => DocumentoExigido.Criar(
        exigidoNaFaseId: exigidoNaFaseId,
        tipoDocumentoOrigemId: Guid.CreateVersion7(),
        tipoDocumentoCodigo: "IDENTIDADE",
        tipoDocumentoNome: "Documento de identidade",
        tipoDocumentoCategoria: "PESSOAL",
        aplicabilidade: Aplicabilidade.Geral,
        obrigatorio: true,
        consequenciaIndeferimento: null,
        condicoes: [],
        basesLegais: basesLegais, idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;

    [Fact(DisplayName = "CA-02: AvaliarConformidade inclui o item 'Base legal das exigências documentais'")]
    public void AvaliarConformidade_IncluiItemBaseLegal()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();

        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario).Should().ContainSingle(i => i.Codigo == "exigencias_base_legal_nao_resolvida");
    }

    [Fact(DisplayName = "CA-03 (semântica vazia): processo sem exigência que determina resultado tem o item satisfeito")]
    public void AvaliarConformidade_SemExigenciaQueDeterminaResultado_ItemSatisfeito()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();

        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario).Single(i => i.Codigo == "exigencias_base_legal_nao_resolvida").Ok.Should().BeTrue();
    }

    [Fact(DisplayName = "Exigência que determina resultado sem base legal reprova o item")]
    public void AvaliarConformidade_ExigenciaSemBaseLegal_ItemReprova()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(ExigenciaObrigatoriaCom(faseId), 0).Value!], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario).Single(i => i.Codigo == "exigencias_base_legal_nao_resolvida").Ok.Should().BeFalse();
    }

    [Fact(DisplayName = "CA-02: exigência com base RESOLVIDO satisfaz o item")]
    public void AvaliarConformidade_ExigenciaComBaseResolvida_ItemSatisfeito()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(ExigenciaObrigatoriaCom(faseId, BaseLegalDe(StatusBaseLegal.Resolvido)), 0).Value!], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario).Single(i => i.Codigo == "exigencias_base_legal_nao_resolvida").Ok.Should().BeTrue();
    }

    [Fact(DisplayName = "Exigência com base só PENDENTE reprova o item")]
    public void AvaliarConformidade_ExigenciaComBasePendente_ItemReprova()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(ExigenciaObrigatoriaCom(faseId, BaseLegalDe(StatusBaseLegal.Pendente)), 0).Value!], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario).Single(i => i.Codigo == "exigencias_base_legal_nao_resolvida").Ok.Should().BeFalse();
    }

    [Fact(DisplayName = "CA-05: reenviar o PUT rebaixando a única base resolvida para PENDENTE volta a reprovar o item")]
    public void AvaliarConformidade_ReenviarRebaixandoUnicaBaseResolvida_VoltaAReprovar()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(ExigenciaObrigatoriaCom(faseId, BaseLegalDe(StatusBaseLegal.Resolvido)), 0).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();
        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario).Single(i => i.Codigo == "exigencias_base_legal_nao_resolvida").Ok.Should().BeTrue();

        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(ExigenciaObrigatoriaCom(faseId, BaseLegalDe(StatusBaseLegal.Pendente)), 0).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario).Single(i => i.Codigo == "exigencias_base_legal_nao_resolvida").Ok.Should().BeFalse();
    }

    [Fact(DisplayName = "CA-05: reenviar o PUT sem a única base resolvida volta a reprovar o item")]
    public void AvaliarConformidade_ReenviarSemUnicaBaseResolvida_VoltaAReprovar()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(ExigenciaObrigatoriaCom(faseId, BaseLegalDe(StatusBaseLegal.Resolvido)), 0).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();
        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario).Single(i => i.Codigo == "exigencias_base_legal_nao_resolvida").Ok.Should().BeTrue();

        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(ExigenciaObrigatoriaCom(faseId), 0).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario).Single(i => i.Codigo == "exigencias_base_legal_nao_resolvida").Ok.Should().BeFalse();
    }

    [Fact(DisplayName = "Publicar bloqueia com ConformidadeInsuficiente quando exigência determina resultado sem base legal — antes de alcançar B-01")]
    public void Publicar_ExigenciaSemBaseLegal_BloqueiaComConformidadeInsuficiente()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(ExigenciaObrigatoriaCom(faseId), 0).Value!], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        Result<VersaoConfiguracao> resultado = processo.Publicar(
            NovosDados(), BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123", TimeProvider.System, ContextoDeContagemDePrazos.SemCalendario);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.ConformidadeInsuficiente");
        resultado.Error.Message.Should().Contain("Base legal das exigências documentais");
    }

    /// <summary>
    /// Lê o timestamp Unix em milissegundos dos 48 bits mais significativos de um Guid v7
    /// (RFC 9562 §5.7). Existe para provar que o id do ato nasce do MESMO instante que a
    /// vigência da versão — não para o domínio usá-lo de volta.
    /// </summary>
    private static DateTimeOffset InstanteDoGuidV7(Guid id)
    {
        Span<byte> bytes = stackalloc byte[16];
        id.TryWriteBytes(bytes, bigEndian: true, out _);

        long milissegundos = 0;
        for (int i = 0; i < 6; i++)
        {
            milissegundos = (milissegundos << 8) | bytes[i];
        }

        return DateTimeOffset.FromUnixTimeMilliseconds(milissegundos);
    }

    /// <summary>Relógio fixo — o teste precisa de um instante determinístico, não de um TimeProvider real.</summary>
    private sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }
}
