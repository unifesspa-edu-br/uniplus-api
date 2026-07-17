namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Xunit;

/// <summary>
/// Paridade bidirecional entre <see cref="DocumentoExigido"/> viva e o item congelado no
/// envelope (Story #554, PR-e, CA-11), e a imunidade do envelope já congelado a mudanças
/// posteriores na configuração viva (CA-12).
/// </summary>
/// <remarks>
/// A golden fixture (<see cref="EnvelopeCanonicoGoldenTests"/>) prova a FORMA de uma
/// publicação isolada; não prova a dimensão TEMPORAL da paridade — o que diverge ENTRE
/// o agregado vivo e um envelope já congelado quando o tempo passa e a configuração
/// muda. É exatamente essa lacuna que as duas contraprovas nomeadas na issue #548
/// cobrem: órfã VIVA (configurada depois da última publicação) e órfã CONGELADA
/// (publicada e depois removida da configuração viva). A projeção é pura (ADR-0109 D6),
/// então esta suíte não precisa de banco — um <see cref="SnapshotCanonico"/> em memória
/// já é o "envelope já congelado" que os testes comparam contra o agregado mutado depois.
/// </remarks>
public sealed class ParidadeExigenciasDocumentaisTests
{
    private static readonly SnapshotPublicacaoCanonicalizer Canonicalizer = new();
    private static readonly string HashFixo = new('a', 64);

    private static DadosEdital DadosDeReferencia() => DadosEdital.Criar(
        numero: "001/2026",
        periodoInscricaoInicio: new DateOnly(2026, 1, 1),
        periodoInscricaoFim: new DateOnly(2026, 1, 31),
        documentoEditalId: Guid.CreateVersion7()).Value!;

    /// <summary>Processo conforme mínimo (etapas/oferta/distribuição/classificação/cronograma) — o par exigido pelo canonicalizador para os blocos reais.</summary>
    private static ProcessoSeletivo NovoProcessoConforme()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Paridade 2026", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria);

        processo.DefinirEtapas([
            EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1),
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

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
            regraDistribuicao: ReferenciaRegra.Criar(RegraDistribuicaoVagasCodigo.Institucional, "v1", HashFixo).Value!,
            regraAjuste: null,
            referenciaDemografica: null,
            modalidades: [modalidade]).Value!;
        processo.DefinirDistribuicaoVagas([distribuicao], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            regraCalculo: ReferenciaRegra.Criar(RegraCalculoCodigo.ClassificacaoImportada, "v1", HashFixo).Value!,
            regraArredondamento: null,
            casasArredondamento: null,
            regraOrdemAlocacao: ReferenciaRegra.Criar(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", HashFixo).Value!,
            nOpcoesAlocacao: 1,
            regrasEliminacao: []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        FaseCronograma fase = FaseCronograma.Criar(
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
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        return processo;
    }

    private static DocumentoExigido ExigenciaGeral(Guid exigidoNaFaseId, string tipoDocumentoCodigo) => DocumentoExigido.Criar(
        exigidoNaFaseId,
        tipoDocumentoOrigemId: Guid.CreateVersion7(),
        tipoDocumentoCodigo: tipoDocumentoCodigo,
        tipoDocumentoNome: tipoDocumentoCodigo,
        tipoDocumentoCategoria: "PESSOAL",
        aplicabilidade: Aplicabilidade.Geral,
        obrigatorio: false,
        consequenciaIndeferimento: null,
        grupoSatisfacaoId: null,
        condicoes: [], basesLegais: [], idadeMaximaEmissao: null, formatoPermitido: null, tamanhoMaximoBytes: null).Value!;

    private static SnapshotCanonico Canonicalizar(ProcessoSeletivo processo) =>
        Canonicalizer.Canonicalizar(new EntradaCanonicalizacao(processo, DadosDeReferencia(), HashFixo));

    private static JsonObject Envelope(SnapshotCanonico snapshot) =>
        JsonNode.Parse(System.Text.Encoding.UTF8.GetString(snapshot.Bytes))!.AsObject();

    private static IEnumerable<Guid> ExigenciasCongeladas(JsonObject envelope) => envelope["documentosExigidos"]!["exigencias"]!.AsArray()
        .Select(static e => Guid.Parse(e!["exigenciaId"]!.GetValue<string>()));

    [Fact(DisplayName = "CA-11: exigência configurada DEPOIS de congelar o envelope é órfã viva — ausente do envelope já congelado")]
    public void ExigenciaConfiguradaDepoisDeCongelar_EOrfaViva()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        Guid faseId = processo.CronogramaFases.Single().Id;
        DocumentoExigido exigenciaA = ExigenciaGeral(faseId, "IDENTIDADE");
        processo.DefinirDocumentosExigidos([exigenciaA], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        SnapshotCanonico congelado = Canonicalizar(processo);
        JsonObject envelope = Envelope(congelado);
        ExigenciasCongeladas(envelope).Should().BeEquivalentTo([exigenciaA.Id]);

        // O envelope acima já está congelado (bytes fixos, como VersaoConfiguracao.Abrir
        // produziria) — o que acontece DEPOIS na configuração viva nunca o altera.
        DocumentoExigido exigenciaB = ExigenciaGeral(faseId, "COMPROVANTE_RESIDENCIA");
        processo.DefinirDocumentosExigidos([exigenciaA, exigenciaB], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        processo.DocumentosExigidos.Select(static d => d.Id).Should().Contain(exigenciaB.Id,
            "pré-condição: a exigência B está viva no agregado");

        ExigenciasCongeladas(envelope).Should().NotContain(exigenciaB.Id,
            "órfã viva (CA-11): configurada depois do congelamento, não tem — e nunca terá — item correspondente NESTE envelope");
    }

    [Fact(DisplayName = "CA-11: exigência removida da configuração viva permanece no envelope já congelado — órfã congelada")]
    public void ExigenciaRemovidaDaConfiguracaoViva_PermaneceOrfaCongelada()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        Guid faseId = processo.CronogramaFases.Single().Id;
        DocumentoExigido exigenciaA = ExigenciaGeral(faseId, "IDENTIDADE");
        DocumentoExigido exigenciaB = ExigenciaGeral(faseId, "COMPROVANTE_RESIDENCIA");
        processo.DefinirDocumentosExigidos([exigenciaA, exigenciaB], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        SnapshotCanonico congeladoV1 = Canonicalizar(processo);
        JsonObject envelopeV1 = Envelope(congeladoV1);
        ExigenciasCongeladas(envelopeV1).Should().BeEquivalentTo([exigenciaA.Id, exigenciaB.Id]);

        // Retificação que remove B (ex.: exigência cadastrada por engano) — só na
        // configuração VIVA; o envelope V1, já congelado, é outro objeto, imutável.
        processo.DefinirDocumentosExigidos([exigenciaA], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        processo.DocumentosExigidos.Select(static d => d.Id).Should().NotContain(exigenciaB.Id,
            "pré-condição: B não está mais viva no agregado");

        ExigenciasCongeladas(envelopeV1).Should().Contain(exigenciaB.Id,
            "órfã congelada (CA-11): o envelope V1 é append-only (ADR-0104) — remover a exigência da configuração " +
            "viva não apaga o item já congelado em versões anteriores");
    }
}
