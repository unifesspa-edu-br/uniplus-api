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
    private const string ResumoDoCorpusRico = "529fc2ed3024293abb25bf256c21be93d25a3fe77e921c21b8bc4c5aa51d69c0";
    private const string ResumoDoCorpusDeReferencia = "6d7d0f5c9988502786ccfd8075a9f738c2901f33961a0f028f520f2bbc9d2765";
    private const string ResumoDoCorpusComCascata = "ada12c2048b4073e2270b88c94f4db56059e3d5faac15c4dd09d221d51a1edba";

    [Fact(DisplayName = "Corpus rico, com dois termos exigidos, tem o resumo do conteúdo congelado igual ao literal")]
    public void CorpusRico_ResumoDoConteudoCongelado_IgualAoLiteral()
    {
        SnapshotCanonico snapshot = CorpusEnvelope.Canonicalizador.Canonicalizar(
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
