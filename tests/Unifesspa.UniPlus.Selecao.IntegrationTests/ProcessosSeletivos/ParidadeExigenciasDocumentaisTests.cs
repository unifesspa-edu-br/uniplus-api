namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Tempo;
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
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

/// <summary>
/// Paridade bidirecional entre <see cref="DocumentoExigido"/> viva e o item congelado no
/// envelope (CA-11), e a imunidade do envelope já congelado a mudanças
/// posteriores na configuração viva (CA-12).
/// </summary>
/// <remarks>
/// A golden fixture (<see cref="EnvelopeCanonicoGoldenTests"/>) prova a FORMA de uma
/// publicação isolada; não prova a dimensão TEMPORAL da paridade — o que diverge ENTRE
/// o agregado vivo e um envelope já congelado quando o tempo passa e a configuração
/// muda. É exatamente essa lacuna que as duas contraprovas abaixo
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
        periodoInscricaoInicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
        periodoInscricaoFim: new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)),
        documentoEditalId: Guid.CreateVersion7()).Value!;

    private static FaseCronograma FaseDeResultadoFinal() => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(
            OrigemId: Guid.CreateVersion7(), Codigo: "RESULTADO_FINAL", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false, ColetaInscricao: true,
            ColetaSolicitacaoIsencao: false)),
        new DecisoesDeCronogramaDaFase(
            Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps),
        HomologacaoDasInscricoesDeTeste.PublicacoesDaFaseQueHomologa(),
        bancasRequeridas: [BancasDeTeste.DaHomologacaoDasInscricoesDaFase()]).Value!;

    /// <summary>Processo conforme mínimo (etapas/oferta/distribuição/classificação/cronograma) — o par exigido pelo canonicalizador para os blocos reais.</summary>
    private static ProcessoSeletivo NovoProcessoConforme()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Paridade 2026", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

        processo.DefinirAtendimentoEspecializado(
            AtendimentoEspecializado.Criar([], [], []).Value!).IsSuccess.Should().BeTrue();

        DefinirDistribuicao(processo);

        DefinirClassificacao(processo);

        DefinirCronograma(processo);

        return processo;
    }

    private static void DefinirDistribuicao(ProcessoSeletivo processo)
    {
        ModalidadeSelecionada modalidade = ModalidadeAmpla();
        ConfiguracaoDistribuicaoVagas distribuicao = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 40, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = ReferenciaRegra.De(RegraDeDistribuicaoDeVagas.Institucional.Codigo(), "v1", HashFixo), Modalidades = [modalidade] },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;
        processo.DefinirDistribuicaoVagas([distribuicao], FatosDeModalidadeDeTeste.DoCatalogo).IsSuccess.Should().BeTrue();
    }

    private static void DefinirClassificacao(ProcessoSeletivo processo)
    {
        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = ReferenciaRegra.De(RegraDeCalculoDaNota.ClassificacaoImportada.Codigo(), "v1", HashFixo),
            RegraOrdemAlocacao = ReferenciaRegra.De(RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria.Codigo(), "v1", HashFixo),
        }).Value!).IsSuccess.Should().BeTrue();
        processo.DefinirOpcoesDeCurso(1).IsSuccess.Should().BeTrue();
    }

    private static void DefinirCronograma(ProcessoSeletivo processo)
    {
        FaseCronograma fase = FaseDeResultadoFinal();
        processo.DefinirCronogramaFases([fase], []).IsSuccess.Should().BeTrue();
        processo.DefinirEtapas([
            EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
                new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: "RESULTADO_FINAL"),
                new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!,
        ]).IsSuccess.Should().BeTrue();
    }

    private static ModalidadeSelecionada ModalidadeAmpla() => ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da("AC", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas) with { RegraRemanejamento = null, BaseLegal = "Res. Unifesspa 532/2021" }, 40).Value!;

    private static DocumentoExigido ExigenciaGeral(Guid exigidoNaFaseId, string tipoDocumentoCodigo) => DocumentoExigido.Criar(
        Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("exigencia_" + tipoDocumentoCodigo.ToLowerInvariant()).Value!,
        new DocumentoExigidoDeclarado(new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), tipoDocumentoCodigo, tipoDocumentoCodigo, "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal),
            new MomentoDaExigencia(exigidoNaFaseId, null, FormulariosDoProcessoDeTeste.Codigo(FormulariosDoProcessoDeTeste.Inscricao)),
            new AplicabilidadeDaExigencia(Aplicabilidade.Geral, []),
            new IndeferimentoDoDocumento(false, null, []),
            RequisitosDoDocumentoDeTeste.Com(FormatosPermitidos.Criar(true, null).Value!),
            null)).Value!;

    private static SnapshotCanonico Canonicalizar(ProcessoSeletivo processo) =>
        Canonicalizer.Canonicalizar(new EntradaCanonicalizacao(processo, DadosDeReferencia(), HashFixo, FusoInstitucional.ZoneId, Retificacao: null, Conformidade: null, DadosDeCatalogo: new DadosDeCatalogoDaConfiguracaoPublicada(ValoresSelecionaveisCongelados: CatalogoDoConjuntoBasico.ComValoresCongelados(), TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(processo), FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao)));

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
        processo.DefinirDocumentosExigidos([RegraDeComprovacao.CriarRegraDeDocumento(exigenciaA, 0).Value!]).IsSuccess.Should().BeTrue();

        SnapshotCanonico congelado = Canonicalizar(processo);
        JsonObject envelope = Envelope(congelado);
        ExigenciasCongeladas(envelope).Should().BeEquivalentTo([exigenciaA.Id]);

        ConferirInclusaoPosterior(processo, faseId, exigenciaA, envelope);
    }

    private static void ConferirInclusaoPosterior(ProcessoSeletivo processo, Guid faseId, DocumentoExigido exigenciaA, JsonObject envelope)
    {
        // O envelope acima já está congelado (bytes fixos, como VersaoConfiguracao.Abrir
        // produziria) — o que acontece DEPOIS na configuração viva nunca o altera.
        DocumentoExigido exigenciaB = ExigenciaGeral(faseId, "COMPROVANTE_RESIDENCIA");
        processo.DefinirDocumentosExigidos(
            [RegraDeComprovacao.CriarRegraDeDocumento(exigenciaA, 0).Value!, RegraDeComprovacao.CriarRegraDeDocumento(exigenciaB, 1).Value!]).IsSuccess.Should().BeTrue();
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
        processo.DefinirDocumentosExigidos(
            [RegraDeComprovacao.CriarRegraDeDocumento(exigenciaA, 0).Value!, RegraDeComprovacao.CriarRegraDeDocumento(exigenciaB, 1).Value!]).IsSuccess.Should().BeTrue();

        SnapshotCanonico congeladoV1 = Canonicalizar(processo);
        JsonObject envelopeV1 = Envelope(congeladoV1);
        ExigenciasCongeladas(envelopeV1).Should().BeEquivalentTo([exigenciaA.Id, exigenciaB.Id]);

        ConferirRemocaoPosterior(processo, exigenciaA, exigenciaB, envelopeV1);
    }

    private static void ConferirRemocaoPosterior(ProcessoSeletivo processo, DocumentoExigido exigenciaA, DocumentoExigido exigenciaB, JsonObject envelopeV1)
    {
        // Retificação que remove B (ex.: exigência cadastrada por engano) — só na
        // configuração VIVA; o envelope V1, já congelado, é outro objeto, imutável.
        processo.DefinirDocumentosExigidos([RegraDeComprovacao.CriarRegraDeDocumento(exigenciaA, 0).Value!]).IsSuccess.Should().BeTrue();
        processo.DocumentosExigidos.Select(static d => d.Id).Should().NotContain(exigenciaB.Id,
            "pré-condição: B não está mais viva no agregado");

        ExigenciasCongeladas(envelopeV1).Should().Contain(exigenciaB.Id,
            "órfã congelada (CA-11): o envelope V1 é append-only (ADR-0104) — remover a exigência da configuração " +
            "viva não apaga o item já congelado em versões anteriores");
    }
}
