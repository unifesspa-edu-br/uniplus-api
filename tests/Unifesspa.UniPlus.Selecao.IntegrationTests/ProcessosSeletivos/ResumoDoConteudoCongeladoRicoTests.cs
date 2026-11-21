namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.EnvelopeDeReferencia;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.NormalizadorDeIdsDoEnvelope;

/// <summary>
/// Fixa o resumo SHA-256 do conteúdo congelado do processo seletivo, o mesmo cálculo que grava
/// <c>HashConfiguracao</c> na versão publicada, ao lado da comparação byte a byte dos arquivos de
/// referência. Não é o resumo da versão do termo de consentimento, que cobre só texto, base legal
/// e forma de aceite. Nos processos de referência os identificadores gerados a cada execução são
/// normalizados antes do resumo, como nos arquivos; no corpus rico os bytes são os próprios do envelope.
/// </summary>
public sealed class ResumoDoConteudoCongeladoRicoTests
{
    private const string ResumoDoCorpusRico = "342fe68fab1289595b4b0b5274d4a2b268c76723d3be99bb1ec5693b4d0a0e45";
    private const string ResumoDoCorpusDeReferencia = "ddb9829c638541d6f5f612a51b4743992959c91428d3dff4ac8057e170b61052";
    private const string ResumoDoCorpusComCascata = "286f088aa8e376742aeb84bb7dd0ebb2615c35e61950e3bed7093478a40f2f4a";

    [Fact(DisplayName = "Corpus rico, com dois termos exigidos, tem o resumo do conteúdo congelado igual ao literal")]
    public void CorpusRico_ResumoDoConteudoCongelado_IgualAoLiteral()
    {
        SnapshotCanonico snapshot = CorpusEnvelope.Codec.Codificar(
            CorpusEnvelopeEntrada.Entrada(CorpusEnvelopeProcessoRico.ProcessoRico()));

        HashCanonicalComputer.ComputeSha256Hex(snapshot.Bytes).Should().Be(ResumoDoCorpusRico);
    }

    [Fact(DisplayName = "Corpus de referência tem o resumo do conteúdo congelado igual ao literal")]
    public void CorpusDeReferencia_ResumoDoConteudoCongelado_IgualAoLiteral() =>
        ResumoDoEnvelopeNormalizado(CanonicalizarReferencia()).Should().Be(ResumoDoCorpusDeReferencia);

    [Fact(DisplayName = "Corpus de referência com cascata tem o resumo do conteúdo congelado igual ao literal")]
    public void CorpusComCascata_ResumoDoConteudoCongelado_IgualAoLiteral() =>
        ResumoDoEnvelopeNormalizado(CanonicalizarReferenciaComCascata()).Should().Be(ResumoDoCorpusComCascata);

    private static string ResumoDoEnvelopeNormalizado(SnapshotCanonico snapshot) =>
        HashCanonicalComputer.ComputeSha256Hex(Encoding.UTF8.GetBytes(NormalizarIds(snapshot.Bytes)));
}
