namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// Prova de fidelidade do bloco <c>divulgacao</c> (UNI-REQ-0050, issue #563, CA-07) —
/// deliberadamente <b>NÃO-CIRCULAR</b>.
/// </summary>
/// <remarks>
/// Duas armadilhas que a formulação óbvia cairia, nesta ordem: (1) canonicalizar duas vezes e
/// comparar os blocos prova <b>determinismo</b>, não fidelidade — um encoder que ignorasse a
/// configuração e sempre emitisse o default passaria nos dois lados; (2)
/// <c>SerializacaoDaDivulgacaoEDaTaxa.SerializarDivulgacao</c> é <c>private</c>, então "recomputar
/// com a mesma função" não é sequer possível — e se fosse, seria a primeira armadilha de novo. O
/// esperado aqui é montado <b>à mão</b>, a partir dos valores que o teste gravou na configuração
/// viva, serializado pelo mesmo perfil canônico, e comparado <b>byte a byte</b> com o bloco
/// extraído do envelope real. O caminho de construção do esperado é independente do caminho que
/// produziu o envelope.
/// </remarks>
public sealed class ConfiguracaoDivulgacaoCanonicalizacaoTests
{
    private static readonly SnapshotPublicacaoCanonicalizer Canonicalizer = new();
    private static readonly string HashFixo = new('a', 64);

    private static FaseCronograma FaseDeResultadoFinal() => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            OrigemId: Guid.CreateVersion7(), Codigo: "RESULTADO_FINAL", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false, ColetaInscricao: true,
            ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(
            Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), FaseConcluinteCodigo: null,
            UnidadeResponsavel: UnidadesDeTeste.Ceps),
        HomologacaoDasInscricoesDeTeste.PublicacoesDaFaseQueHomologa(resultado: "RESULTADO_FINAL"),
        bancasRequeridas: []).Value!;

    private static ProcessoSeletivo NovoProcessoConforme()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Divulgação 2026", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

        processo.DefinirCronogramaFases([FaseDeResultadoFinal()],
            []).IsSuccess.Should().BeTrue();
        DefinirEtapaEAtendimento(processo);

        DefinirDistribuicao(processo);

        DefinirClassificacaoEOpcoes(processo);

        return processo;
    }

    private static void DefinirClassificacaoEOpcoes(ProcessoSeletivo processo)
    {
        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = ReferenciaRegra.De(RegraCalculoCodigo.ClassificacaoImportada, "v1", HashFixo),
            RegraOrdemAlocacao = ReferenciaRegra.De(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "v1", HashFixo),
        }).Value!).IsSuccess.Should().BeTrue();
        processo.DefinirOpcoesDeCurso(1).IsSuccess.Should().BeTrue();
    }

    private static void DefinirEtapaEAtendimento(ProcessoSeletivo processo)
    {
        processo.DefinirEtapas([
            EtapaProcesso.Criar(
                new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: "RESULTADO_FINAL"),
                new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!,
        ]).IsSuccess.Should().BeTrue();
        processo.DefinirAtendimentoEspecializado(
            AtendimentoEspecializado.Criar([], [], []).Value!).IsSuccess.Should().BeTrue();
    }

    private static void DefinirDistribuicao(ProcessoSeletivo processo)
    {
        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas,
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), null, "Res. Unifesspa 532/2021", quantidadeDeclarada: 40).Value!;
        processo.DefinirDistribuicaoVagas([ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 40, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = ReferenciaRegra.De(RegraDistribuicaoVagasCodigo.Institucional, "v1", HashFixo), Modalidades = [modalidade] },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!], FatosDeModalidadeDeTeste.DoCatalogo).IsSuccess.Should().BeTrue();
    }

    private static DadosEdital DadosDeReferencia() => DadosEdital.Criar(
        "001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)), new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)), Guid.CreateVersion7()).Value!;

    private static JsonNode ExtrairBlocoDivulgacao(byte[] bytes) =>
        JsonNode.Parse(Encoding.UTF8.GetString(bytes))!["divulgacao"]!;

    private static byte[] SerializarParaComparacao(JsonNode bloco) =>
        PerfilCanonicoV1.Instancia.Serializar(new JsonObject { ["v"] = bloco.DeepClone() });

    [Fact(DisplayName = "CA-07: sem configuração, o bloco divulgacao congela o default minimizado — bytes idênticos ao oráculo montado à parte")]
    public void SemConfiguracao_CongelaODefaultMinimizado()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();

        SnapshotCanonico canonico = Canonicalizer.Canonicalizar(
            new EntradaCanonicalizacao(processo, DadosDeReferencia(), HashFixo, FusoInstitucional.ZoneId, Retificacao: null, Conformidade: null, DadosDeCatalogo: new DadosDeCatalogoDaConfiguracaoPublicada(ValoresSelecionaveisCongelados: CatalogoDoConjuntoBasico.ComValoresCongelados(), TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(processo), FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao)));

        JsonObject esperado = new()
        {
            ["camposPublicos"] = new JsonArray(JsonValue.Create(ConfiguracaoDivulgacao.NumeroInscricao)),
            ["regraNomeAbreviado"] = null,
            ["justificativa"] = null,
        };

        SerializarParaComparacao(ExtrairBlocoDivulgacao(canonico.Bytes)).Should().Equal(SerializarParaComparacao(esperado));
    }

    [Fact(DisplayName = "CA-07: com nome_abreviado e justificativa decombinada, o bloco congela a regra vigente e a justificativa em NFC — bytes idênticos ao oráculo montado à parte")]
    public void ComNomeAbreviadoEJustificativaDecombinada_CongelaRegraEJustificativaNormalizada()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();

        DefinirDivulgacaoComJustificativaDecombinada(processo);

        SnapshotCanonico canonico = Canonicalizer.Canonicalizar(
            new EntradaCanonicalizacao(processo, DadosDeReferencia(), HashFixo, FusoInstitucional.ZoneId, Retificacao: null, Conformidade: null, DadosDeCatalogo: new DadosDeCatalogoDaConfiguracaoPublicada(ValoresSelecionaveisCongelados: CatalogoDoConjuntoBasico.ComValoresCongelados(), TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(processo), FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao)));

        JsonObject esperado = BlocoEsperado("Divulgação ampliada.");

        SerializarParaComparacao(ExtrairBlocoDivulgacao(canonico.Bytes)).Should().Equal(SerializarParaComparacao(esperado));
    }

    private static void DefinirDivulgacaoComJustificativaDecombinada(ProcessoSeletivo processo)
    {
        // "ç" e "ã" DECOMBINADOS (c + U+0327, a + U+0303) nas bordas com espaço — a forma
        // pré-composta no esperado é o que fecha a prova de que ALGUM ponto do caminho
        // (entidade e/ou encoder) aplica NFC: uma entrada só-ASCII produziria os mesmos bytes
        // com ou sem normalização, e não testaria nada.
        string justificativaDecombinada = "  Divulgação ampliada.  ";

        processo.DefinirConfiguracaoDivulgacao(
            ConfiguracaoDivulgacao.Criar(["numero_inscricao", "nome_abreviado"], justificativaDecombinada).Value!).IsSuccess.Should().BeTrue();
    }

    private static JsonObject BlocoEsperado(string justificativaPreComposta)
    {
        // Literal, escrito à mão — independente de qualquer chamada de normalização do encoder
        // ou da entidade.
        return new JsonObject()
        {
            ["camposPublicos"] = new JsonArray(
                JsonValue.Create(ConfiguracaoDivulgacao.NomeAbreviado), JsonValue.Create(ConfiguracaoDivulgacao.NumeroInscricao)),
            ["regraNomeAbreviado"] = RegrasDeNomeAbreviado.Vigente,
            ["justificativa"] = justificativaPreComposta,
        };
    }
}
