namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Xunit;

/// <summary>
/// Prova de fidelidade do bloco <c>divulgacao</c> (UNI-REQ-0050, issue #563, CA-07) —
/// deliberadamente <b>NÃO-CIRCULAR</b>.
/// </summary>
/// <remarks>
/// Duas armadilhas que a formulação óbvia cairia, nesta ordem: (1) canonicalizar duas vezes e
/// comparar os blocos prova <b>determinismo</b>, não fidelidade — um encoder que ignorasse a
/// configuração e sempre emitisse o default passaria nos dois lados; (2)
/// <c>SnapshotPublicacaoCanonicalizer.SerializarDivulgacao</c> é <c>private</c>, então "recomputar
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

    private static ProcessoSeletivo NovoProcessoConforme()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Divulgação 2026", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

        processo.DefinirEtapas([
            EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva").Value!, peso: 1m, ordem: 1).Value!,
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo,
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, [], null, "Res. Unifesspa 532/2021", quantidadeDeclarada: 40).Value!;
        processo.DefinirDistribuicaoVagas([ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), 40, 1m, ReferenciaRegra.Criar(RegraDistribuicaoVagasCodigo.Institucional, "v1", HashFixo).Value!,
            null, null, [modalidade]).Value!], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            ReferenciaRegra.Criar(RegraCalculoCodigo.ClassificacaoImportada, "v1", HashFixo).Value!, null, null,
            ReferenciaRegra.Criar(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", HashFixo).Value!, 1, [], baseadoEmEnem: false).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirCronogramaFases([FaseCronograma.Criar(
            ordem: 1, faseCanonicaOrigemId: Guid.CreateVersion7(), codigo: "RESULTADO_FINAL", donoInstitucional: "CEPS",
            origemData: OrigemDataFase.Propria, agrupaEtapas: true, permiteComplementacao: false, produzResultado: true,
            resultadoDefinitivo: true, coletaInscricao: true,
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            atoProduzidoCodigo: "RESULTADO_FINAL", atoProduzidoEfeitoIrreversivel: false, bancasRequeridas: [], regraRecurso: null).Value!],
            [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        return processo;
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
            new EntradaCanonicalizacao(processo, DadosDeReferencia(), HashFixo, FusoInstitucional.ZoneId));

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

        // "ç" e "ã" DECOMBINADOS (c + U+0327, a + U+0303) nas bordas com espaço — a forma
        // pré-composta no esperado é o que fecha a prova de que ALGUM ponto do caminho
        // (entidade e/ou encoder) aplica NFC: uma entrada só-ASCII produziria os mesmos bytes
        // com ou sem normalização, e não testaria nada.
        string justificativaDecombinada = "  Divulgação ampliada.  ";
        string justificativaPreComposta = "Divulgação ampliada.";

        processo.DefinirConfiguracaoDivulgacao(
            ConfiguracaoDivulgacao.Criar(["numero_inscricao", "nome_abreviado"], justificativaDecombinada).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        SnapshotCanonico canonico = Canonicalizer.Canonicalizar(
            new EntradaCanonicalizacao(processo, DadosDeReferencia(), HashFixo, FusoInstitucional.ZoneId));

        // Literal, escrito à mão — independente de qualquer chamada de normalização do encoder
        // ou da entidade.
        JsonObject esperado = new()
        {
            ["camposPublicos"] = new JsonArray(
                JsonValue.Create(ConfiguracaoDivulgacao.NomeAbreviado), JsonValue.Create(ConfiguracaoDivulgacao.NumeroInscricao)),
            ["regraNomeAbreviado"] = RegrasDeNomeAbreviado.Vigente,
            ["justificativa"] = justificativaPreComposta,
        };

        SerializarParaComparacao(ExtrairBlocoDivulgacao(canonico.Bytes)).Should().Equal(SerializarParaComparacao(esperado));
    }
}
