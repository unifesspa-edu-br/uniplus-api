namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Xunit;

/// <summary>
/// <c>dataReferenciaFatos</c> — a <see cref="DateOnly"/> resolvida e congelada no
/// envelope 1.2 a partir da política <see cref="ReferenciaTemporalFatos"/> (Story #554,
/// PR #903, B-03). Complementa <c>ProcessoSeletivoPublicarTests</c> (que prova os bloqueios
/// de publicação quando a resolução falha) com o caso em que ela FUNCIONA: cada
/// congelamento resolve a política vigente NO MOMENTO em que congela, e uma retificação
/// que muda a política depois não reabre nem altera o que já foi congelado.
/// </summary>
/// <remarks>A projeção é pura (ADR-0109 D6) — esta suíte não precisa de banco.</remarks>
public sealed class ReferenciaTemporalFatosResolucaoTests
{
    private static readonly SnapshotPublicacaoCanonicalizer Canonicalizer = new();
    private static readonly string HashFixo = new('a', 64);

    private static DadosEdital DadosDeReferencia() => DadosEdital.Criar(
        numero: "001/2026",
        periodoInscricaoInicio: new DateOnly(2026, 1, 1),
        periodoInscricaoFim: new DateOnly(2026, 1, 31),
        documentoEditalId: Guid.CreateVersion7()).Value!;

    private static ProcessoSeletivo NovoProcessoConformeComGatilhoPorFaixaEtaria(out Guid faseId)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS ReferenciaTemporalFatos 2026", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria);

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
        faseId = fase.Id;

        CondicaoGatilho condicao = CondicaoGatilho.Criar(
            0, "FAIXA_ETARIA", Operador.MaiorIgual, JsonSerializer.SerializeToElement(18)).Value!;
        DocumentoExigidoBaseLegal baseLegal = DocumentoExigidoBaseLegal.Criar(
            "Lei 12.711/2012, art. 3º", TipoAbrangencia.InternaEdital, StatusBaseLegal.Resolvido, null).Value!;
        DocumentoExigido exigencia = DocumentoExigido.Criar(
            fase.Id,
            tipoDocumentoOrigemId: Guid.CreateVersion7(),
            tipoDocumentoCodigo: "DECLARACAO_MAIORIDADE",
            tipoDocumentoNome: "Declaração de maioridade",
            tipoDocumentoCategoria: "PESSOAL",
            aplicabilidade: Aplicabilidade.Condicional,
            obrigatorio: true,
            consequenciaIndeferimento: null,
            grupoSatisfacaoId: null,
            condicoes: [condicao], basesLegais: [baseLegal], idadeMaximaEmissao: null, formatoPermitido: null, tamanhoMaximoBytes: null).Value!;
        processo.DefinirDocumentosExigidos([exigencia], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        return processo;
    }

    private static SnapshotCanonico Canonicalizar(ProcessoSeletivo processo) =>
        Canonicalizer.Canonicalizar(new EntradaCanonicalizacao(processo, DadosDeReferencia(), HashFixo));

    private static string? DataReferenciaFatos(SnapshotCanonico snapshot)
    {
        JsonObject envelope = JsonNode.Parse(System.Text.Encoding.UTF8.GetString(snapshot.Bytes))!.AsObject();
        return envelope["documentosExigidos"]!["dataReferenciaFatos"]?.GetValue<string>();
    }

    [Fact(DisplayName = "B-03: uma retificação que muda a política de ReferenciaTemporalFatos não altera a dataReferenciaFatos já congelada de uma versão anterior")]
    public void RetificacaoMudaPolitica_PreservaDataReferenciaFatosCongeladaDaVersaoAnterior()
    {
        ProcessoSeletivo processo = NovoProcessoConformeComGatilhoPorFaixaEtaria(out _);
        processo.DefinirReferenciaTemporalFatos(
            ReferenciaTemporalFatos.Criar(ReferenciaTipo.DataEspecifica, new DateOnly(2026, 1, 15), null).Value!,
            PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        SnapshotCanonico congeladoV1 = Canonicalizar(processo);
        DataReferenciaFatos(congeladoV1).Should().Be("2026-01-15");

        // Retificação: a política muda para uma DATA_ESPECIFICA diferente.
        processo.DefinirReferenciaTemporalFatos(
            ReferenciaTemporalFatos.Criar(ReferenciaTipo.DataEspecifica, new DateOnly(2026, 2, 20), null).Value!,
            PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        SnapshotCanonico congeladoV2 = Canonicalizar(processo);
        DataReferenciaFatos(congeladoV2).Should().Be("2026-02-20",
            "pré-condição: o congelamento SEGUINTE resolve a política vigente no momento em que congela");

        DataReferenciaFatos(congeladoV1).Should().Be("2026-01-15",
            "B-03: cada VersaoConfiguracao congela sua PRÓPRIA política — o envelope V1, já congelado, é " +
            "imutável (ADR-0104); a retificação que mudou a política para produzir V2 não o reabre nem o altera");
    }
}
