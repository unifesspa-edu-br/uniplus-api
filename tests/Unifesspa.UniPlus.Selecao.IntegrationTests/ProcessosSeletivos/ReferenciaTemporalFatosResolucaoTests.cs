namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Testes.Compartilhado;

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
        periodoInscricaoInicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
        periodoInscricaoFim: new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)),
        documentoEditalId: Guid.CreateVersion7()).Value!;

    private static FaseCronograma FaseDeResultadoFinal() => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        new OrigemCanonicaDaFase(
            OrigemId: Guid.CreateVersion7(), Codigo: "RESULTADO_FINAL", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false, ColetaInscricao: true,
            ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(
            Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    private static ProcessoSeletivo NovoProcessoConformeComGatilhoPorFaixaEtaria(out Guid faseId)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS ReferenciaTemporalFatos 2026", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

        DefinirAtendimentoEDistribuicao(processo);
        DefinirClassificacao(processo);

        FaseCronograma fase = FaseDeResultadoFinal();
        processo.DefinirCronogramaFases([fase], []).IsSuccess.Should().BeTrue();
        DefinirEtapa(processo);
        faseId = fase.Id;

        DefinirExigencia(processo, faseId);

        return processo;
    }

    private static void DefinirAtendimentoEDistribuicao(ProcessoSeletivo processo)
    {
        processo.DefinirAtendimentoEspecializado(
            AtendimentoEspecializado.Criar([], [], []).Value!).IsSuccess.Should().BeTrue();
        DefinirDistribuicao(processo);
    }

    private static void DefinirDistribuicao(ProcessoSeletivo processo)
    {
        ModalidadeSelecionada modalidade = ModalidadeAmpla();
        ConfiguracaoDistribuicaoVagas distribuicao = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 40, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = ReferenciaRegra.De(RegraDistribuicaoVagasCodigo.Institucional, "v1", HashFixo), Modalidades = [modalidade] },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;
        processo.DefinirDistribuicaoVagas([distribuicao], FatosDeModalidadeDeTeste.DoCatalogo).IsSuccess.Should().BeTrue();
    }

    private static ModalidadeSelecionada ModalidadeAmpla() => ModalidadeSelecionada.Criar(
        modalidadeOrigemId: Guid.CreateVersion7(), codigo: "AC", descricao: null,
        naturezaLegal: NaturezaLegalModalidade.Ampla,
        composicaoVagas: ComposicaoVagasModalidade.RestanteDasVagasOfertadas,
        composicaoOrigemCodigo: null, regraRemanejamento: RegraRemanejamentoModalidade.Nenhuma,
        remanejamentoDestino: null, remanejamentoPar: null, remanejamentoFallback: null,
        regraDeAplicabilidade: RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla),
        acaoQuandoIndeferido: null, baseLegal: "Res. Unifesspa 532/2021", quantidadeDeclarada: 40).Value!;

    private static void DefinirClassificacao(ProcessoSeletivo processo)
    {
        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = ReferenciaRegra.De(RegraCalculoCodigo.ClassificacaoImportada, "v1", HashFixo),
            RegraOrdemAlocacao = ReferenciaRegra.De(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "v1", HashFixo),
        }).Value!).IsSuccess.Should().BeTrue();
        processo.DefinirOpcoesDeCurso(1).IsSuccess.Should().BeTrue();
    }

    private static void DefinirEtapa(ProcessoSeletivo processo)
    {
        processo.DefinirEtapas([
            EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
                new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: "RESULTADO_FINAL"),
                new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!,
        ]).IsSuccess.Should().BeTrue();
    }

    private static void DefinirExigencia(ProcessoSeletivo processo, Guid faseId)
    {
        CondicaoGatilho condicao = CondicaoGatilho.Criar(
            0, "FAIXA_ETARIA", Operador.MaiorIgual, JsonSerializer.SerializeToElement(18)).Value!;
        DocumentoExigidoBaseLegal baseLegal = DocumentoExigidoBaseLegal.Criar(
            "Lei 12.711/2012, art. 3º", TipoAbrangencia.InternaEdital, StatusBaseLegal.Resolvido, null).Value!;
        DocumentoExigido exigencia = ExigenciaDeMaioridade(faseId, condicao, baseLegal);
        processo.DefinirDocumentosExigidos([RegraDeComprovacao.CriarRegraDeDocumento(exigencia, 0).Value!]).IsSuccess.Should().BeTrue();
    }

    private static DocumentoExigido ExigenciaDeMaioridade(Guid faseId, CondicaoGatilho condicao, DocumentoExigidoBaseLegal baseLegal) =>
        DocumentoExigido.Criar(
            Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("exigencia_declaracao_maioridade").Value!,
            new DocumentoExigidoDeclarado(new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), "DECLARACAO_MAIORIDADE", "Declaração de maioridade", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal),
                new MomentoDaExigencia(faseId, null, FinalidadeFormulario.Inscricao),
                new AplicabilidadeDaExigencia(Aplicabilidade.Condicional, [condicao]),
                new IndeferimentoDoDocumento(true, null, [baseLegal]),
                RequisitosDoDocumentoDeTeste.Com(FormatosPermitidos.Criar(true, null).Value!),
                null)).Value!;

    private static SnapshotCanonico Canonicalizar(ProcessoSeletivo processo) =>
        Canonicalizer.Canonicalizar(new EntradaCanonicalizacao(processo, DadosDeReferencia(), HashFixo, FusoInstitucional.ZoneId, Retificacao: null, Conformidade: null, DadosDeCatalogo: new DadosDeCatalogoDaConfiguracaoPublicada(ValoresSelecionaveisCongelados: CatalogoDoConjuntoBasico.ComValoresCongelados(), TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(processo), FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao)));

    private static string? DataReferenciaFatos(SnapshotCanonico snapshot)
    {
        JsonObject envelope = JsonNode.Parse(System.Text.Encoding.UTF8.GetString(snapshot.Bytes))!.AsObject();
        return envelope["documentosExigidos"]!["dataDeReferenciaDosDados"]?.GetValue<string>();
    }

    [Fact(DisplayName = "B-03: uma retificação que muda a política de ReferenciaTemporalFatos não altera a dataDeReferenciaDosDados já congelada de uma versão anterior")]
    public void RetificacaoMudaPolitica_PreservaDataReferenciaFatosCongeladaDaVersaoAnterior()
    {
        ProcessoSeletivo processo = NovoProcessoConformeComGatilhoPorFaixaEtaria(out _);
        processo.DefinirReferenciaTemporalFatos(
            ReferenciaTemporalFatos.Criar(ReferenciaTipo.DataEspecifica, new DateOnly(2026, 1, 15), null).Value!).IsSuccess.Should().BeTrue();

        SnapshotCanonico congeladoV1 = Canonicalizar(processo);
        DataReferenciaFatos(congeladoV1).Should().Be("2026-01-15");

        ConferirDataDaRetificacao(processo);

        DataReferenciaFatos(congeladoV1).Should().Be("2026-01-15",
            "B-03: cada VersaoConfiguracao congela sua PRÓPRIA política — o envelope V1, já congelado, é " +
            "imutável (ADR-0104); a retificação que mudou a política para produzir V2 não o reabre nem o altera");
    }

    private static void ConferirDataDaRetificacao(ProcessoSeletivo processo)
    {
        // Retificação: a política muda para uma DATA_ESPECIFICA diferente.
        processo.DefinirReferenciaTemporalFatos(
            ReferenciaTemporalFatos.Criar(ReferenciaTipo.DataEspecifica, new DateOnly(2026, 2, 20), null).Value!).IsSuccess.Should().BeTrue();

        SnapshotCanonico congeladoV2 = Canonicalizar(processo);
        DataReferenciaFatos(congeladoV2).Should().Be("2026-02-20",
            "pré-condição: o congelamento SEGUINTE resolve a política vigente no momento em que congela");
    }
}
