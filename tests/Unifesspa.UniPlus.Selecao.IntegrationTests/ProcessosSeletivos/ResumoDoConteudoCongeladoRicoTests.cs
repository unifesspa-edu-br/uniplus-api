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
    private const string ResumoDoCorpusRico = "1dec3d11c214390306f05f462df67d937be97e12e2e697e62aedc5b62f941f61";
    private const string ResumoDoCorpusDeReferencia = "397af5880623add7d7a2552b61b448cbf899253a23b940b17b307ac4af2bc197";
    private const string ResumoDoCorpusComCascata = "8b07ed4832b935b5374a0acb4cd1ce8e9726dfa1985cde5dd5df8ce90a7931e3";

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
