namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text;
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
        periodoInscricaoInicio: new DateOnly(2026, 1, 1),
        periodoInscricaoFim: new DateOnly(2026, 1, 31),
        documentoEditalId: Guid.CreateVersion7()).Value!;

    /// <summary>
    /// Monta um processo minimamente conforme (CA-07: etapas, oferta de
    /// atendimento, distribuição de vagas, classificação) — o par exigido
    /// pelo gate de conformidade de <see cref="ProcessoSeletivo.Publicar"/>.
    /// </summary>
    private static ProcessoSeletivo NovoProcessoConforme()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria);

        processo.DefinirEtapas([
            EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1),
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
            regrasEliminacao: []).Value!;
        processo.DefinirClassificacao(classificacao, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirCronogramaFases([FaseConforme()], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

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
            dados, BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123", TimeProvider.System);

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
            NovosDados(), BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123", new RelogioFixo(instante));

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
            dados, BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123", TimeProvider.System);

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
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS incompleto", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria);
        // Nenhuma dimensão obrigatória definida — Etapas/Atendimento/Distribuição/Classificação ausentes.

        Result<VersaoConfiguracao> resultado = processo.Publicar(
            NovosDados(), BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123", TimeProvider.System);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.ConformidadeInsuficiente");
        processo.Status.Should().Be(StatusProcesso.Rascunho, "publicação recusada não transita o status");
        processo.DequeueDomainEvents().Should().BeEmpty("nada é enfileirado numa publicação recusada");
    }

    [Fact(DisplayName = "Lifecycle_TransicaoInvalidaRecusada — publicar processo já publicado é recusado (CA-09)")]
    public void Publicar_ProcessoJaPublicado_RecusaTransicaoInvalida()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        processo.Publicar(NovosDados(), BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123", TimeProvider.System)
            .IsSuccess.Should().BeTrue();
        processo.DequeueDomainEvents();

        Result<VersaoConfiguracao> segundaTentativa = processo.Publicar(
            NovosDados(), BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123", TimeProvider.System);

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
    public void DefinirX_ProcessoPublicado_RecusaMutacao(string dimensao)
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        processo.Publicar(NovosDados(), BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123", TimeProvider.System)
            .IsSuccess.Should().BeTrue();

        Result resultado = dimensao switch
        {
            "etapas" => processo.DefinirEtapas([EtapaProcesso.Criar("Nova Etapa", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1)], PrecondicaoIfMatch.Ausente),
            "ofertaAtendimento" => processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente),
            "distribuicaoVagas" => processo.DefinirDistribuicaoVagas([], PrecondicaoIfMatch.Ausente),
            "bonusRegional" => processo.DefinirBonusRegional(null, PrecondicaoIfMatch.Ausente),
            "criteriosDesempate" => processo.DefinirCriteriosDesempate([], PrecondicaoIfMatch.Ausente),
            "classificacao" => processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
                ReferenciaRegra.Criar(RegraCalculoCodigo.ClassificacaoImportada, "v1", HashFixo).Value!,
                null, null,
                ReferenciaRegra.Criar(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", HashFixo).Value!,
                1, []).Value!, PrecondicaoIfMatch.Ausente),
            "cronogramaFases" => processo.DefinirCronogramaFases([FaseConforme()], [], PrecondicaoIfMatch.Ausente),
            _ => throw new InvalidOperationException("Dimensão desconhecida."),
        };

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
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
