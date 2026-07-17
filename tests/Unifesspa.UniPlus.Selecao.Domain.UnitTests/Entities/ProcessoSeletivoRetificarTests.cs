namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura de <see cref="ProcessoSeletivo.Retificar"/> (RN08, ADR-0101/0103/0104):
/// a retificação é uma <b>relação entre atos</b>, não um tipo — o novo ato emenda o
/// ato criador da versão corrente, sucede-a na cadeia linear e exige motivo; a versão
/// anterior fica intocada e o status permanece Publicado.
/// </summary>
/// <remarks>
/// Não há mais entidade <c>Edital</c> a inspecionar: o documento é o ato publicado, e
/// vive em <c>Publicacoes</c>. O que estes testes verificam é a única coisa que a
/// Seleção decide sobre ele — o seu identificador, e o ato que ele emenda.
/// </remarks>
public sealed class ProcessoSeletivoRetificarTests
{
    private static readonly string HashFixo = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];
    private static readonly byte[] BytesCanonicos = Encoding.UTF8.GetBytes(new JsonObject { ["status"] = "ok" }.ToJsonString());

    private static DadosEdital NovosDados() => DadosEdital.Criar(
        numero: "001/2026",
        periodoInscricaoInicio: new DateOnly(2026, 1, 1),
        periodoInscricaoFim: new DateOnly(2026, 1, 31),
        documentoEditalId: Guid.CreateVersion7()).Value!;

    // Relógio manual: os testes avançam (ou regridem) o instante explicitamente
    // quando o cenário exige. Datas de publicação iguais entre atos do mesmo
    // processo são um estado válido (ADR-0104) — a data documental não ordena.
    private static RelogioManual Relogio() => new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    // A data que o DOCUMENTO declara (ADR-0108) não aparece mais aqui: ela é atributo do ato,
    // que é de Publicações (ADR-0103/0105), e viaja direto para lá na mensagem durável. O
    // agregado não a recebe nem a guarda — ela nunca ordenou coisa alguma.

    /// <summary>
    /// Versão de configuração avulsa, sem vínculo com nenhum processo real —
    /// usada só no teste que recusa retificação de um processo em rascunho:
    /// a guarda de transição de status barra ANTES de qualquer uso do
    /// conteúdo da versão, então o valor em si é irrelevante ali; o que
    /// importa é satisfazer o parâmetro não nulo de <see cref="ProcessoSeletivo.Retificar"/>.
    /// </summary>
    private static VersaoConfiguracao VersaoQualquer() => VersaoConfiguracao.Abrir(
        Guid.CreateVersion7(),
        BytesCanonicos,
        "1.0",
        "canonical-json/sha256@v1",
        Guid.CreateVersion7(),
        HashFixo,
        "user-sub-123",
        Relogio().GetUtcNow());

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

        ConfiguracaoClassificacao classificacao = ConfiguracaoClassificacao.Criar(
            regraCalculo: ReferenciaRegra.Criar(RegraCalculoCodigo.ClassificacaoImportada, "v1", HashFixo).Value!,
            regraArredondamento: null,
            casasArredondamento: null,
            regraOrdemAlocacao: ReferenciaRegra.Criar(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", HashFixo).Value!,
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

    private static ProcessoSeletivo NovoProcessoPublicado(RelogioManual clock, out VersaoConfiguracao versaoAbertura)
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        Result<VersaoConfiguracao> publicacao = processo.Publicar(
            NovosDados(), BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123", clock);
        publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);
        versaoAbertura = publicacao.Value!;
        processo.DequeueDomainEvents();
        return processo;
    }

    [Fact(DisplayName = "Retificar processo publicado emite um ato que emenda o ato criador da versão corrente, com motivo")]
    public void Retificar_ProcessoPublicado_EmiteAtoQueEmendaOVigente()
    {
        RelogioManual clock = Relogio();
        ProcessoSeletivo processo = NovoProcessoPublicado(clock, out VersaoConfiguracao versaoAbertura);
        clock.Avancar(TimeSpan.FromMinutes(1));

        Result<VersaoConfiguracao> resultado = processo.Retificar(
            NovosDados(), versaoAbertura, BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123",
            motivo: "Correção do prazo de inscrição", clock: clock);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.Status.Should().Be(StatusProcesso.Publicado, "retificar não altera o status Publicado");

        VersaoConfiguracao versao = resultado.Value!;
        versao.NumeroVersao.Should().Be(2);
        versao.AtoCriadorId.Should().NotBe(versaoAbertura.AtoCriadorId, "a retificação é um ato NOVO — o passado não se muta");
        versao.AtoCriadorRetificaId.Should().Be(
            versaoAbertura.AtoCriadorId,
            "o alvo é o ato criador da versão corrente, e o servidor o infere — o cliente nunca o informa (ADR-0101)");
    }

    [Fact(DisplayName = "A retificação de um ato NÃO muda o tipo do ato: o que a marca é a relação, e nada além dela")]
    public void Retificar_NaoCarregaMarcaDeTipo_SoARelacao()
    {
        RelogioManual clock = Relogio();
        ProcessoSeletivo processo = NovoProcessoPublicado(clock, out VersaoConfiguracao versaoAbertura);
        clock.Avancar(TimeSpan.FromMinutes(1));

        Result<VersaoConfiguracao> resultado = processo.Retificar(
            NovosDados(), versaoAbertura, BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123",
            motivo: "Correção do anexo II", clock: clock);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);

        // A ÚNICA diferença entre a versão da abertura e a da retificação é a presença do ato
        // emendado. Não há campo de "natureza", nem rótulo, nem enum: uma convocação retificada
        // continua convocação, e o tipo do ato vem do catálogo de Publicações, declarado pelo
        // operador (ADR-0103). Se um dia voltar a existir um atributo aqui que diga "isto é uma
        // retificação", este teste é o que o denuncia.
        versaoAbertura.AtoCriadorRetificaId.Should().BeNull("a raiz da cadeia não emenda ninguém");
        resultado.Value!.AtoCriadorRetificaId.Should().NotBeNull("a retificação emenda — e é só isso que a distingue");

        typeof(VersaoConfiguracao).GetProperties()
            .Select(p => p.Name)
            .Should().NotContain(
                nome => nome.Contains("Natureza", StringComparison.OrdinalIgnoreCase)
                    || nome.Contains("TipoAto", StringComparison.OrdinalIgnoreCase),
                "a configuração congelada não carrega o tipo do ato: saber o que um ato É pertence a Publicações");
    }

    [Fact(DisplayName = "Lifecycle — retificar processo ainda em rascunho é recusado (CA-09)")]
    public void Retificar_ProcessoRascunho_RecusaTransicaoInvalida()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();

        Result<VersaoConfiguracao> resultado = processo.Retificar(
            NovosDados(), VersaoQualquer(), BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123",
            motivo: "qualquer", clock: Relogio());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.TransicaoInvalida");
        processo.DequeueDomainEvents().Should().BeEmpty();
    }

    [Fact(DisplayName = "Retificação sem motivo é recusada — a relação é o par (ato emendado, motivo), completo ou nenhum")]
    public void Retificar_SemMotivo_Recusa()
    {
        RelogioManual clock = Relogio();
        ProcessoSeletivo processo = NovoProcessoPublicado(clock, out VersaoConfiguracao versaoAbertura);
        clock.Avancar(TimeSpan.FromMinutes(1));

        Result<VersaoConfiguracao> resultado = processo.Retificar(
            NovosDados(), versaoAbertura, BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123",
            motivo: "   ", clock: clock);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.MotivoRetificacaoObrigatorio");
        processo.DequeueDomainEvents().Should().BeEmpty("nenhuma versão é criada quando a relação está incompleta");
    }

    [Fact(DisplayName = "Segunda retificação empilha na CABEÇA da cadeia (abertura→R1→R2), não na raiz (CA-06)")]
    public void Retificar_SegundaRetificacao_EmpilhaNaCabeca()
    {
        RelogioManual clock = Relogio();
        ProcessoSeletivo processo = NovoProcessoPublicado(clock, out VersaoConfiguracao versaoAbertura);
        clock.Avancar(TimeSpan.FromMinutes(1));

        Result<VersaoConfiguracao> primeira = processo.Retificar(
            NovosDados(), versaoAbertura, BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123",
            motivo: "primeira retificação", clock: clock);
        primeira.IsSuccess.Should().BeTrue(primeira.Error?.Message);
        clock.Avancar(TimeSpan.FromMinutes(1));

        // A segunda retificação emenda o ato que criou a versão CORRENTE (R1), não a abertura:
        // é o que a prática faz — o aviso que prorroga um prazo já prorrogado cita o aviso
        // anterior, não o edital original (ADR-0103).
        Result<VersaoConfiguracao> segunda = processo.Retificar(
            NovosDados(), primeira.Value!, BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123",
            motivo: "segunda retificação", clock: clock);

        segunda.IsSuccess.Should().BeTrue(segunda.Error?.Message);
        primeira.Value!.AtoCriadorRetificaId.Should().Be(versaoAbertura.AtoCriadorId, "R1 emenda a abertura");
        segunda.Value!.AtoCriadorRetificaId.Should().Be(
            primeira.Value!.AtoCriadorId,
            "R2 emenda R1 — cada ato é emendado no máximo uma vez, e a cadeia é linear");
        segunda.Value!.NumeroVersao.Should().Be(3, "o topo da cadeia de configuração avança com a última retificação");
    }

    [Fact(DisplayName = "Retificar — versão corrente de OUTRO processo é recusada: a cadeia não atravessa certames")]
    public void Retificar_VersaoDeOutroProcesso_Recusa()
    {
        RelogioManual clock = Relogio();
        ProcessoSeletivo processo = NovoProcessoPublicado(clock, out _);
        clock.Avancar(TimeSpan.FromMinutes(1));

        Result<VersaoConfiguracao> resultado = processo.Retificar(
            NovosDados(), VersaoQualquer(), BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123",
            motivo: "Correção de datas", clock: clock);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("VersaoConfiguracao.VersaoAnteriorDeOutroProcesso");
        processo.DequeueDomainEvents().Should().BeEmpty("nada é mutado quando a versão informada não é deste certame");
    }

    [Fact(DisplayName = "Relógio regredido: o id do ato da retificação NÃO ordena antes do ato que ele emenda (Guid v7, ADR-0032)")]
    public void Retificar_RelogioRegride_IdDoAtoNaoRegride()
    {
        RelogioManual clock = Relogio();
        ProcessoSeletivo processo = NovoProcessoPublicado(clock, out VersaoConfiguracao versaoAbertura);

        // O relógio regride 10 minutos entre a abertura e a retificação (ajuste NTP em
        // degrau). Suceder já ancorava a VIGÊNCIA na da anterior; o id do ato tem de nascer
        // do mesmo instante ancorado.
        clock.Avancar(TimeSpan.FromMinutes(-10));

        Result<VersaoConfiguracao> retificacao = processo.Retificar(
            NovosDados(), versaoAbertura, BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123",
            motivo: "retificação sob relógio regredido", clock: clock);

        retificacao.IsSuccess.Should().BeTrue(retificacao.Error?.Message);

        // O Guid v7 carrega o timestamp nos 48 bits MAIS SIGNIFICATIVOS (RFC 9562 §5.7), e é
        // por eles que o Postgres ordena `uuid` — a ordem bytewise big-endian de que a
        // paginação por keyset depende (ADR-0032). Se o id do ato novo nascesse do relógio
        // CRU, o seu timestamp seria 10 minutos ANTERIOR ao do ato que ele emenda, e a cadeia
        // apareceria invertida na listagem. (Comparar os `Guid` em C# não serviria: o layout
        // do tipo é misto, e a ordem de `Comparer<Guid>` não é a do banco.)
        DateTimeOffset instanteDaAbertura = InstanteDoGuidV7(versaoAbertura.AtoCriadorId);
        DateTimeOffset instanteDaRetificacao = InstanteDoGuidV7(retificacao.Value!.AtoCriadorId);

        instanteDaRetificacao.Should().BeOnOrAfter(
            instanteDaAbertura,
            "o ato que emenda nunca ordena antes do ato emendado — o instante do id é ancorado, como o da vigência");

        retificacao.Value!.VigenteAPartirDe.Should().Be(
            versaoAbertura.VigenteAPartirDe,
            "id e vigência descrevem o MESMO instante: uma leitura do relógio, uma âncora só");
    }

    /// <summary>
    /// Lê o timestamp Unix em milissegundos dos 48 bits mais significativos de um Guid v7
    /// (RFC 9562 §5.7) — a mesma grandeza pela qual o Postgres ordena a coluna <c>uuid</c>.
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

    [Fact(DisplayName = "Retificar — relógio que regride não move o alvo: quem ordena a cadeia é a versão, não o tempo")]
    public void Retificar_RelogioRegride_AlvoVemDaCadeiaDeVersoes()
    {
        RelogioManual clock = Relogio();
        ProcessoSeletivo processo = NovoProcessoPublicado(clock, out VersaoConfiguracao versaoAbertura);

        // O relógio regride (ajuste NTP em degrau, troca de hora do host). Isso não pode mover
        // a cadeia: quem ordena as versões é a vigência, e ela nunca precede a da anterior.
        clock.Avancar(TimeSpan.FromMinutes(-10));
        Result<VersaoConfiguracao> primeira = processo.Retificar(
            NovosDados(), versaoAbertura, BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123",
            motivo: "primeira retificação", clock: clock);
        primeira.IsSuccess.Should().BeTrue(primeira.Error?.Message);
        primeira.Value!.VigenteAPartirDe.Should().Be(
            versaoAbertura.VigenteAPartirDe,
            "a vigência da sucessora é ancorada na anterior quando o relógio regride — nunca precede o passado");

        // A segunda retificação continua possível, e emenda R1 — o ato que criou a versão
        // corrente. Ordenar o alvo pelo tempo tornaria uma cadeia perfeitamente linear
        // irretificável.
        Result<VersaoConfiguracao> segunda = processo.Retificar(
            NovosDados(), primeira.Value!, BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123",
            motivo: "segunda retificação", clock: clock);

        segunda.IsSuccess.Should().BeTrue(segunda.Error?.Message);
        segunda.Value!.AtoCriadorRetificaId.Should().Be(
            primeira.Value!.AtoCriadorId,
            "o alvo é o ato criador da versão corrente — a cadeia manda, não o relógio");
        segunda.Value!.NumeroVersao.Should().Be(3);
    }

    // ── Story #554 (PR-a) — guarda fail-closed (B-01) via FecharRetificacao ──

    [Fact(DisplayName = "B-01: fechar retificação com exigência configurada durante a sessão é bloqueado")]
    public void FecharRetificacao_ExigenciaConfiguradaNaSessao_BloqueiaPorGuardaFailClosed()
    {
        RelogioManual clock = Relogio();
        ProcessoSeletivo processo = NovoProcessoPublicado(clock, out VersaoConfiguracao versaoAbertura);
        clock.Avancar(TimeSpan.FromMinutes(1));

        Result<RascunhoRetificacao> abertura = processo.AbrirRetificacao(
            "Incluir exigência documental", versaoAbertura, "user-sub-123", clock.GetUtcNow());
        abertura.IsSuccess.Should().BeTrue(abertura.Error?.Message);

        Guid faseId = processo.CronogramaFases.Single().Id;
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
            condicoes: []).Value!;
        processo.DefinirDocumentosExigidos([exigencia], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue("mutar a configuração viva durante a sessão é permitido — só o FECHAMENTO é bloqueado pela B-01");

        clock.Avancar(TimeSpan.FromMinutes(1));
        Result<VersaoConfiguracao> resultado = processo.FecharRetificacao(
            NovosDados(), versaoAbertura, BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123",
            PrecondicaoIfMatch.Curinga, clock);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.ExigenciasDocumentaisNaoMaterializadas");
        processo.Rascunho.Should().NotBeNull("o fechamento recusado não encerra a sessão — a configuração continua editável");
    }

    /// <summary>
    /// Relógio manual determinístico — evita depender de
    /// <c>Microsoft.Extensions.TimeProvider.Testing</c> na camada de teste do
    /// Domain (que só referencia Domain + Kernel). Permite dar instantes
    /// distintos à abertura e à retificação (unicidade de data_publicacao).
    /// </summary>
    private sealed class RelogioManual(DateTimeOffset inicio) : TimeProvider
    {
        private DateTimeOffset _agora = inicio;

        public override DateTimeOffset GetUtcNow() => _agora;

        public void Avancar(TimeSpan delta) => _agora = _agora.Add(delta);
    }
}
