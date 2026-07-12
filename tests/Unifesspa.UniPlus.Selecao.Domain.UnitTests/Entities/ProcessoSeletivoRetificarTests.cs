namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura de <see cref="ProcessoSeletivo.Retificar"/> e
/// <see cref="Edital.EmitirRetificacao"/> (RN08, Story #759 T5 #786,
/// ADR-0101) — novo Edital de retificação vinculado ao vigente, motivo
/// obrigatório, novo snapshot, snapshot anterior intocado, status permanece
/// Publicado. Mapa de testes de #759: <c>Retificacao_NovoEditalSnapshotMotivo</c>,
/// <c>Edital_ContratoAberturaRetificacao</c>, <c>Retificacao_MesmoProcesso</c>.
/// </summary>
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
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU);

        processo.DefinirEtapas([
            EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1),
        ]).IsSuccess.Should().BeTrue();

        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([], [], []).Value!).IsSuccess.Should().BeTrue();

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
            baseLegal: "Res. Unifesspa 532/2021").Value!;
        ConfiguracaoDistribuicaoVagas distribuicao = ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: Guid.CreateVersion7(),
            voBase: 40,
            pr: 1m,
            regraDistribuicao: regraDistribuicao,
            referenciaDemografica: null,
            modalidades: [modalidade]).Value!;
        processo.DefinirDistribuicaoVagas([distribuicao]).IsSuccess.Should().BeTrue();

        ConfiguracaoClassificacao classificacao = ConfiguracaoClassificacao.Criar(
            regraCalculo: ReferenciaRegra.Criar(RegraCalculoCodigo.ClassificacaoImportada, "v1", HashFixo).Value!,
            regraArredondamento: null,
            casasArredondamento: null,
            regraOrdemAlocacao: ReferenciaRegra.Criar(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", HashFixo).Value!,
            nOpcoesAlocacao: 1,
            regrasEliminacao: []).Value!;
        processo.DefinirClassificacao(classificacao).IsSuccess.Should().BeTrue();

        return processo;
    }

    private static ProcessoSeletivo NovoProcessoPublicado(RelogioManual clock, out Edital abertura, out VersaoConfiguracao versaoAbertura)
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        Result<PublicacaoResultado> publicacao = processo.Publicar(
            NovosDados(), BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123", clock);
        publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);
        abertura = publicacao.Value!.Edital;
        versaoAbertura = publicacao.Value!.Versao;
        return processo;
    }

    [Fact(DisplayName = "Retificacao_NovoEditalSnapshotMotivo — retificar processo publicado emite Edital de retificação vinculado ao vigente")]
    public void Retificar_ProcessoPublicado_EmiteEditalRetificacaoVinculado()
    {
        RelogioManual clock = Relogio();
        ProcessoSeletivo processo = NovoProcessoPublicado(clock, out Edital abertura, out VersaoConfiguracao versaoAbertura);
        clock.Avancar(TimeSpan.FromMinutes(1));

        Result<PublicacaoResultado> resultado = processo.Retificar(
            NovosDados(), versaoAbertura, BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123",
            motivo: "Correção do prazo de inscrição", clock: clock);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.Status.Should().Be(StatusProcesso.Publicado, "retificar não altera o status Publicado");
        processo.Editais.Should().HaveCount(2);

        Edital retificacao = resultado.Value!.Edital;
        retificacao.Natureza.Should().Be(NaturezaEdital.Retificacao);
        retificacao.EditalRetificadoId.Should().Be(abertura.Id);
        retificacao.MotivoRetificacao.Should().Be("Correção do prazo de inscrição");
        retificacao.DataPublicacao.Should().NotBeNull().And.NotBe(abertura.DataPublicacao);
        resultado.Value!.Versao.AtoCriadorId.Should().Be(retificacao.Id);
    }

    [Fact(DisplayName = "Retificacao_NovoEditalSnapshotMotivo — retificação emite evento com os identificadores do novo Edital/snapshot")]
    public void Retificar_ProcessoPublicado_EmiteEventoComNovosIdentificadores()
    {
        RelogioManual clock = Relogio();
        ProcessoSeletivo processo = NovoProcessoPublicado(clock, out _, out VersaoConfiguracao versaoAbertura);
        processo.ClearDomainEvents(); // descarta o evento da abertura
        clock.Avancar(TimeSpan.FromMinutes(1));

        Result<PublicacaoResultado> resultado = processo.Retificar(
            NovosDados(), versaoAbertura, BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123",
            "Ajuste de vaga", clock);

        resultado.IsSuccess.Should().BeTrue();
        Domain.Events.ProcessoPublicadoEvent evento = processo.DomainEvents
            .OfType<Domain.Events.ProcessoPublicadoEvent>().Should().ContainSingle().Subject;
        evento.EditalId.Should().Be(resultado.Value!.Edital.Id);
        evento.SnapshotPublicacaoId.Should().Be(resultado.Value!.Versao.Id);
    }

    [Fact(DisplayName = "Lifecycle — retificar processo ainda em rascunho é recusado (CA-09)")]
    public void Retificar_ProcessoRascunho_RecusaTransicaoInvalida()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();

        Result<PublicacaoResultado> resultado = processo.Retificar(
            NovosDados(), VersaoQualquer(), BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123",
            motivo: "qualquer", clock: Relogio());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.TransicaoInvalida");
        processo.Editais.Should().BeEmpty();
    }

    [Fact(DisplayName = "Retificacao_MesmoProcesso — segunda retificação sucede o vigente, formando cadeia linear abertura→R1→R2 (CA-06)")]
    public void Retificar_SegundaRetificacao_SucedeVigente_CadeiaLinear()
    {
        RelogioManual clock = Relogio();
        ProcessoSeletivo processo = NovoProcessoPublicado(clock, out Edital abertura, out VersaoConfiguracao versaoAbertura);
        clock.Avancar(TimeSpan.FromMinutes(1));

        Result<PublicacaoResultado> primeira = processo.Retificar(
            NovosDados(), versaoAbertura, BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123",
            motivo: "primeira retificação", clock: clock);
        primeira.IsSuccess.Should().BeTrue(primeira.Error?.Message);
        clock.Avancar(TimeSpan.FromMinutes(1));

        // A segunda retificação sucede automaticamente o vigente (a 1ª
        // retificação), não a abertura — o alvo é resolvido pela raiz. A
        // versão que ela sucede também é a da 1ª retificação, devolvida no
        // resultado anterior — não a da abertura.
        Result<PublicacaoResultado> segunda = processo.Retificar(
            NovosDados(), primeira.Value!.Versao, BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123",
            motivo: "segunda retificação", clock: clock);

        segunda.IsSuccess.Should().BeTrue(segunda.Error?.Message);
        primeira.Value!.Edital.EditalRetificadoId.Should().Be(abertura.Id, "R1 sucede a abertura");
        segunda.Value!.Edital.EditalRetificadoId.Should().Be(primeira.Value!.Edital.Id, "R2 sucede R1 — cada Edital retificado uma única vez");
        processo.Editais.Should().HaveCount(3);
        segunda.Value!.Versao.NumeroVersao.Should().Be(3, "o topo da cadeia de configuração avança com a última retificação");
        segunda.Value!.Versao.AtoCriadorId.Should().Be(segunda.Value!.Edital.Id, "a versão do topo foi criada pelo último ato");
    }

    [Fact(DisplayName = "Edital_ContratoAberturaRetificacao — EmitirRetificacao sem motivo é recusado")]
    public void EmitirRetificacao_MotivoVazio_Recusa()
    {
        Result<Edital> resultado = Edital.EmitirRetificacao(
            Guid.CreateVersion7(), NovosDados(), Guid.CreateVersion7(), motivo: "   ", instante: Relogio().GetUtcNow());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Edital.MotivoRetificacaoObrigatorio");
    }

    [Fact(DisplayName = "Edital_ContratoAberturaRetificacao — EmitirRetificacao sem edital retificado é recusado")]
    public void EmitirRetificacao_SemEditalRetificado_Recusa()
    {
        Result<Edital> resultado = Edital.EmitirRetificacao(
            Guid.CreateVersion7(), NovosDados(), editalRetificadoId: Guid.Empty, motivo: "motivo", instante: Relogio().GetUtcNow());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Edital.EditalRetificadoObrigatorio");
    }

    [Fact(DisplayName = "Edital_ContratoAberturaRetificacao — EmitirRetificacao preenche os dois campos de retificação")]
    public void EmitirRetificacao_Valida_PreencheContratoRetificacao()
    {
        Guid retificadoId = Guid.CreateVersion7();

        Result<Edital> resultado = Edital.EmitirRetificacao(
            Guid.CreateVersion7(), NovosDados(), retificadoId, motivo: "  Adequação a decisão superveniente  ", instante: Relogio().GetUtcNow());

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Natureza.Should().Be(NaturezaEdital.Retificacao);
        resultado.Value!.EditalRetificadoId.Should().Be(retificadoId);
        resultado.Value!.MotivoRetificacao.Should().Be("Adequação a decisão superveniente", "o motivo é normalizado com Trim");
    }

    [Fact(DisplayName = "Retificar — versão corrente de OUTRO processo é recusada: a cadeia não atravessa certames")]
    public void Retificar_VersaoDeOutroProcesso_Recusa()
    {
        RelogioManual clock = Relogio();
        ProcessoSeletivo processo = NovoProcessoPublicado(clock, out _, out _);
        clock.Avancar(TimeSpan.FromMinutes(1));

        Result<PublicacaoResultado> resultado = processo.Retificar(
            NovosDados(), VersaoQualquer(), BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123",
            motivo: "Correção de datas", clock: clock);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("VersaoConfiguracao.VersaoAnteriorDeOutroProcesso");
        processo.Editais.Should().ContainSingle("nada é mutado quando a versão informada não é deste certame");
    }

    [Fact(DisplayName = "Retificar — versão corrente cujo ato criador não está entre os Editais do processo é recusada")]
    public void Retificar_VersaoComAtoCriadorDesconhecido_Recusa()
    {
        RelogioManual clock = Relogio();
        ProcessoSeletivo processo = NovoProcessoPublicado(clock, out _, out VersaoConfiguracao versaoAbertura);
        clock.Avancar(TimeSpan.FromMinutes(1));

        // Uma versão do MESMO processo, mas criada por um ato que este processo
        // não conhece: não há Edital a retificar, e emitir a retificação
        // apontando para um documento inexistente deixaria a cadeia pendurada
        // no vazio.
        VersaoConfiguracao versaoForaDaCadeia = VersaoConfiguracao.Abrir(
            processo.Id,
            BytesCanonicos,
            "1.0",
            "canonical-json/sha256@v1",
            Guid.CreateVersion7(),
            HashFixo,
            "user-sub-123",
            clock.GetUtcNow());
        versaoForaDaCadeia.AtoCriadorId.Should().NotBe(
            versaoAbertura.AtoCriadorId,
            "pré-condição do teste: o ato criador tem de ser outro");

        // Drena o evento da publicação para que o assert final fale só da
        // retificação recusada.
        processo.DequeueDomainEvents();

        Result<PublicacaoResultado> resultado = processo.Retificar(
            NovosDados(), versaoForaDaCadeia, BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123",
            motivo: "Correção de datas", clock: clock);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("VersaoConfiguracao.CadeiaQuebrada");
        processo.Editais.Should().ContainSingle("o Edital de retificação não é emitido quando a cadeia não fecha");
        processo.DequeueDomainEvents().Should().BeEmpty("nenhum evento é enfileirado numa retificação recusada");
    }

    [Fact(DisplayName = "Retificar — relógio que regride não move o alvo: a retificação sucede o ato criador da versão corrente, não o Edital de maior data")]
    public void Retificar_RelogioRegride_AlvoVemDaCadeiaDeVersoes()
    {
        RelogioManual clock = Relogio();
        ProcessoSeletivo processo = NovoProcessoPublicado(clock, out Edital abertura, out VersaoConfiguracao versaoAbertura);

        // Primeira retificação num instante ANTERIOR ao da abertura — o que um
        // ajuste NTP em degrau produz. A data documental do Edital passa a ordenar
        // ao contrário da cadeia: o "vigente por data" volta a ser a abertura.
        clock.Avancar(TimeSpan.FromMinutes(-10));
        Result<PublicacaoResultado> primeira = processo.Retificar(
            NovosDados(), versaoAbertura, BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123",
            motivo: "primeira retificação", clock: clock);
        primeira.IsSuccess.Should().BeTrue(primeira.Error?.Message);
        primeira.Value!.Edital.DataPublicacao.Should().BeBefore(
            abertura.DataPublicacao!.Value,
            "pré-condição do teste: a data DOCUMENTAL da retificação regrediu para antes da abertura — é essa divergência que não pode decidir nada");
        primeira.Value!.Versao.VigenteAPartirDe.Should().Be(
            versaoAbertura.VigenteAPartirDe,
            "a vigência da sucessora é ancorada na anterior quando o relógio regride — nunca precede o passado");

        // A segunda retificação continua possível, e sucede R1 — o ato que criou a
        // versão corrente —, não a abertura. Ordenar o alvo por data tornaria uma
        // cadeia perfeitamente linear irretificável.
        Result<PublicacaoResultado> segunda = processo.Retificar(
            NovosDados(), primeira.Value!.Versao, BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123",
            motivo: "segunda retificação", clock: clock);

        segunda.IsSuccess.Should().BeTrue(segunda.Error?.Message);
        segunda.Value!.Edital.EditalRetificadoId.Should().Be(
            primeira.Value!.Edital.Id,
            "o alvo é o ato criador da versão corrente — a cadeia manda, não o relógio");
        segunda.Value!.Versao.NumeroVersao.Should().Be(3);
        segunda.Value!.Versao.AtoCriadorRetificaId.Should().Be(primeira.Value!.Edital.Id);
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
