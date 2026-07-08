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
/// #785) — gate de conformidade, transição atômica, congelamento do
/// <see cref="SnapshotPublicacao"/> e bloqueio de mutação pós-publicação
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
        processo.DefinirClassificacao(classificacao).IsSuccess.Should().BeTrue();

        return processo;
    }

    [Fact(DisplayName = "Publicacao_AtomicaStatusESnapshot — processo conforme publica, congela snapshot e transita status")]
    public void Publicar_ProcessoConforme_TransitaStatusECongelaSnapshot()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        DadosEdital dados = NovosDados();

        Result<PublicacaoResultado> resultado = processo.Publicar(
            dados, BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123", TimeProvider.System);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.Status.Should().Be(StatusProcesso.Publicado);
        processo.Editais.Should().ContainSingle();
        processo.Editais.Single().Should().Be(resultado.Value!.Edital);
        resultado.Value!.Edital.Natureza.Should().Be(NaturezaEdital.Abertura);
        resultado.Value!.Edital.DataPublicacao.Should().NotBeNull();
        resultado.Value!.Snapshot.EditalId.Should().Be(resultado.Value!.Edital.Id);
        resultado.Value!.Snapshot.HashEdital.Should().Be(HashFixo);
    }

    [Fact(DisplayName = "Publicacao_AtomicaStatusESnapshot — evento carrega os identificadores forenses completos")]
    public void Publicar_ProcessoConforme_EmiteEventoComIdentificadoresCompletos()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        DadosEdital dados = NovosDados();

        Result<PublicacaoResultado> resultado = processo.Publicar(
            dados, BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123", TimeProvider.System);

        resultado.IsSuccess.Should().BeTrue();
        Domain.Events.ProcessoPublicadoEvent evento = processo.DomainEvents
            .OfType<Domain.Events.ProcessoPublicadoEvent>().Should().ContainSingle().Subject;

        evento.ProcessoSeletivoId.Should().Be(processo.Id);
        evento.EditalId.Should().Be(resultado.Value!.Edital.Id);
        evento.SnapshotPublicacaoId.Should().Be(resultado.Value!.Snapshot.Id);
        evento.HashConfiguracao.Should().Be(resultado.Value!.Snapshot.HashConfiguracao);
        evento.HashEdital.Should().Be(HashFixo);
    }

    [Fact(DisplayName = "Publicacao_RecusaSemParametrosObrigatorios — processo sem etapas recusa com checklist de pendências (CA-03)")]
    public void Publicar_SemEtapas_RecusaComConformidadeInsuficiente()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS incompleto", TipoProcesso.SiSU);
        // Nenhuma dimensão obrigatória definida — Etapas/Atendimento/Distribuição/Classificação ausentes.

        Result<PublicacaoResultado> resultado = processo.Publicar(
            NovosDados(), BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123", TimeProvider.System);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.ConformidadeInsuficiente");
        processo.Status.Should().Be(StatusProcesso.Rascunho, "publicação recusada não transita o status");
        processo.Editais.Should().BeEmpty();
    }

    [Fact(DisplayName = "Lifecycle_TransicaoInvalidaRecusada — publicar processo já publicado é recusado (CA-09)")]
    public void Publicar_ProcessoJaPublicado_RecusaTransicaoInvalida()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        processo.Publicar(NovosDados(), BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123", TimeProvider.System)
            .IsSuccess.Should().BeTrue();

        Result<PublicacaoResultado> segundaTentativa = processo.Publicar(
            NovosDados(), BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123", TimeProvider.System);

        segundaTentativa.IsFailure.Should().BeTrue();
        segundaTentativa.Error!.Code.Should().Be("ProcessoSeletivo.TransicaoInvalida");
        processo.Editais.Should().ContainSingle("a segunda tentativa não deve emitir um segundo Edital");
    }

    [Theory(DisplayName = "PosPublicacao_MutacaoBloqueada_422 — todo Definir* recusa mutação após publicação (CA-04)")]
    [InlineData("etapas")]
    [InlineData("ofertaAtendimento")]
    [InlineData("distribuicaoVagas")]
    [InlineData("bonusRegional")]
    [InlineData("criteriosDesempate")]
    [InlineData("classificacao")]
    public void DefinirX_ProcessoPublicado_RecusaMutacao(string dimensao)
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        processo.Publicar(NovosDados(), BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123", TimeProvider.System)
            .IsSuccess.Should().BeTrue();

        Result resultado = dimensao switch
        {
            "etapas" => processo.DefinirEtapas([EtapaProcesso.Criar("Nova Etapa", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1)]),
            "ofertaAtendimento" => processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!),
            "distribuicaoVagas" => processo.DefinirDistribuicaoVagas([]),
            "bonusRegional" => processo.DefinirBonusRegional(null),
            "criteriosDesempate" => processo.DefinirCriteriosDesempate([]),
            "classificacao" => processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
                ReferenciaRegra.Criar(RegraCalculoCodigo.ClassificacaoImportada, "v1", HashFixo).Value!,
                null, null,
                ReferenciaRegra.Criar(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", HashFixo).Value!,
                1, []).Value!),
            _ => throw new InvalidOperationException("Dimensão desconhecida."),
        };

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
    }
}
