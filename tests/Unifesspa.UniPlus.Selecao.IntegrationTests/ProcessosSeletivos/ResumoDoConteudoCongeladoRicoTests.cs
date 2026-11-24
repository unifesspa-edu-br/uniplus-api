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
/// normalizados antes do resumo, como nos arquivos; no exemplo rico os bytes são os próprios do envelope.
/// </summary>
public sealed class ResumoDoConteudoCongeladoRicoTests
{
    private const string ResumoDoExemploRico = "b5df935121bd496e40bb266c2d2a7e201e79343b7ccbefad8359058b18fe92d9";
    private const string ResumoDoExemploDeReferencia = "c1d889c4a834b508950f7019d201edc1bc2a4cd4ab98ee981096ce8cd89b90fb";
    private const string ResumoDoExemploComCascata = "37203f8099f389f9a0af941030b87da1a23b600a670b07775526dd017c409d07";

    [Fact(DisplayName = "Exemplo rico, com dois termos exigidos, tem o resumo do conteúdo congelado igual ao literal")]
    public void ExemploRico_ResumoDoConteudoCongelado_IgualAoLiteral()
    {
        SnapshotCanonico snapshot = ExemploDeEnvelope.Canonicalizador.Canonicalizar(
            ExemploDeEnvelopeEntrada.Entrada(ExemploDeEnvelopeProcessoRico.ProcessoRico()));

        HashCanonicalComputer.ComputeSha256Hex(snapshot.Bytes).Should().Be(ResumoDoExemploRico);
    }

    [Fact(DisplayName = "Exemplo de referência tem o resumo do conteúdo congelado igual ao literal")]
    public void ExemploDeReferencia_ResumoDoConteudoCongelado_IgualAoLiteral() =>
        ResumoDoEnvelopeNormalizado(CanonicalizarReferencia()).Should().Be(ResumoDoExemploDeReferencia);

    [Fact(DisplayName = "Exemplo de referência com cascata tem o resumo do conteúdo congelado igual ao literal")]
    public void ExemploComCascata_ResumoDoConteudoCongelado_IgualAoLiteral() =>
        ResumoDoEnvelopeNormalizado(CanonicalizarReferenciaComCascata()).Should().Be(ResumoDoExemploComCascata);

    private static string ResumoDoEnvelopeNormalizado(SnapshotCanonico snapshot) =>
        HashCanonicalComputer.ComputeSha256Hex(Encoding.UTF8.GetBytes(NormalizarIds(snapshot.Bytes)));
}
