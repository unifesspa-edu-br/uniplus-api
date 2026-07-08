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

    // Relógio que avança a cada leitura — a data de publicação da retificação
    // precisa diferir da abertura (unicidade por processo, ux_editais_processo_data_publicacao).
    private static RelogioManual Relogio() => new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

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

    private static ProcessoSeletivo NovoProcessoPublicado(RelogioManual clock, out Edital abertura)
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        Result<PublicacaoResultado> publicacao = processo.Publicar(
            NovosDados(), BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123", clock);
        publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);
        abertura = publicacao.Value!.Edital;
        return processo;
    }

    [Fact(DisplayName = "Retificacao_NovoEditalSnapshotMotivo — retificar processo publicado emite Edital de retificação vinculado ao vigente")]
    public void Retificar_ProcessoPublicado_EmiteEditalRetificacaoVinculado()
    {
        RelogioManual clock = Relogio();
        ProcessoSeletivo processo = NovoProcessoPublicado(clock, out Edital abertura);
        clock.Avancar(TimeSpan.FromMinutes(1));

        Result<PublicacaoResultado> resultado = processo.Retificar(
            NovosDados(), BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123",
            editalRetificadoId: abertura.Id, motivo: "Correção do prazo de inscrição", clock: clock);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.Status.Should().Be(StatusProcesso.Publicado, "retificar não altera o status Publicado");
        processo.Editais.Should().HaveCount(2);

        Edital retificacao = resultado.Value!.Edital;
        retificacao.Natureza.Should().Be(NaturezaEdital.Retificacao);
        retificacao.EditalRetificadoId.Should().Be(abertura.Id);
        retificacao.MotivoRetificacao.Should().Be("Correção do prazo de inscrição");
        retificacao.DataPublicacao.Should().NotBeNull().And.NotBe(abertura.DataPublicacao);
        resultado.Value!.Snapshot.EditalId.Should().Be(retificacao.Id);
    }

    [Fact(DisplayName = "Retificacao_NovoEditalSnapshotMotivo — retificação emite evento com os identificadores do novo Edital/snapshot")]
    public void Retificar_ProcessoPublicado_EmiteEventoComNovosIdentificadores()
    {
        RelogioManual clock = Relogio();
        ProcessoSeletivo processo = NovoProcessoPublicado(clock, out Edital abertura);
        processo.ClearDomainEvents(); // descarta o evento da abertura
        clock.Avancar(TimeSpan.FromMinutes(1));

        Result<PublicacaoResultado> resultado = processo.Retificar(
            NovosDados(), BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123",
            abertura.Id, "Ajuste de vaga", clock);

        resultado.IsSuccess.Should().BeTrue();
        Domain.Events.ProcessoPublicadoEvent evento = processo.DomainEvents
            .OfType<Domain.Events.ProcessoPublicadoEvent>().Should().ContainSingle().Subject;
        evento.EditalId.Should().Be(resultado.Value!.Edital.Id);
        evento.SnapshotPublicacaoId.Should().Be(resultado.Value!.Snapshot.Id);
    }

    [Fact(DisplayName = "Lifecycle — retificar processo ainda em rascunho é recusado (CA-09)")]
    public void Retificar_ProcessoRascunho_RecusaTransicaoInvalida()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();

        Result<PublicacaoResultado> resultado = processo.Retificar(
            NovosDados(), BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123",
            editalRetificadoId: Guid.CreateVersion7(), motivo: "qualquer", clock: Relogio());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.TransicaoInvalida");
        processo.Editais.Should().BeEmpty();
    }

    [Fact(DisplayName = "Retificacao_MesmoProcesso — retificar referenciando edital que não é o vigente é recusado (CA-06)")]
    public void Retificar_EditalRetificadoNaoVigente_Recusa()
    {
        RelogioManual clock = Relogio();
        ProcessoSeletivo processo = NovoProcessoPublicado(clock, out _);
        clock.Avancar(TimeSpan.FromMinutes(1));

        // Id arbitrário (ex.: edital de outro processo) — não é o vigente.
        Result<PublicacaoResultado> resultado = processo.Retificar(
            NovosDados(), BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123",
            editalRetificadoId: Guid.CreateVersion7(), motivo: "motivo", clock: clock);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.EditalRetificadoInvalido");
        processo.Editais.Should().ContainSingle("a retificação recusada não emite um segundo Edital");
    }

    [Fact(DisplayName = "Retificacao — segunda retificação deve suceder a primeira, não a abertura (cadeia linear)")]
    public void Retificar_SegundaRetificacao_ReferenciandoAbertura_Recusa()
    {
        RelogioManual clock = Relogio();
        ProcessoSeletivo processo = NovoProcessoPublicado(clock, out Edital abertura);
        clock.Avancar(TimeSpan.FromMinutes(1));
        processo.Retificar(NovosDados(), BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123",
            abertura.Id, "primeira retificação", clock).IsSuccess.Should().BeTrue();
        clock.Avancar(TimeSpan.FromMinutes(1));

        // Tenta suceder a abertura de novo — mas o vigente agora é a 1ª retificação.
        Result<PublicacaoResultado> resultado = processo.Retificar(
            NovosDados(), BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123",
            abertura.Id, "segunda retificação", clock);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.EditalRetificadoInvalido");
    }

    [Fact(DisplayName = "Edital_ContratoAberturaRetificacao — EmitirRetificacao sem motivo é recusado")]
    public void EmitirRetificacao_MotivoVazio_Recusa()
    {
        Result<Edital> resultado = Edital.EmitirRetificacao(
            Guid.CreateVersion7(), NovosDados(), Guid.CreateVersion7(), motivo: "   ", clock: Relogio());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Edital.MotivoRetificacaoObrigatorio");
    }

    [Fact(DisplayName = "Edital_ContratoAberturaRetificacao — EmitirRetificacao sem edital retificado é recusado")]
    public void EmitirRetificacao_SemEditalRetificado_Recusa()
    {
        Result<Edital> resultado = Edital.EmitirRetificacao(
            Guid.CreateVersion7(), NovosDados(), editalRetificadoId: Guid.Empty, motivo: "motivo", clock: Relogio());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Edital.EditalRetificadoObrigatorio");
    }

    [Fact(DisplayName = "Edital_ContratoAberturaRetificacao — EmitirRetificacao preenche os dois campos de retificação")]
    public void EmitirRetificacao_Valida_PreencheContratoRetificacao()
    {
        Guid retificadoId = Guid.CreateVersion7();

        Result<Edital> resultado = Edital.EmitirRetificacao(
            Guid.CreateVersion7(), NovosDados(), retificadoId, motivo: "  Adequação a decisão superveniente  ", clock: Relogio());

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Natureza.Should().Be(NaturezaEdital.Retificacao);
        resultado.Value!.EditalRetificadoId.Should().Be(retificadoId);
        resultado.Value!.MotivoRetificacao.Should().Be("Adequação a decisão superveniente", "o motivo é normalizado com Trim");
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
