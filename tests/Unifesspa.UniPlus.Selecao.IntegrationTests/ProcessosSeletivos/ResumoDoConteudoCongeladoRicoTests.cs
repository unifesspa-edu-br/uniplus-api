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
    private const string ResumoDoExemploRico = "af035462b823f088fee181184a10777a1b62afd25233854b015c97674f763417";
    private const string ResumoDoExemploDeReferencia = "83af0aeb282f993342f69e1b1a2c3c97079c6c7d9de5c62fa7451434bdb2dbd5";
    private const string ResumoDoExemploComCascata = "880d784d0276cd0f0320788d5c6cafb0f37f3494076ab006c67d181071dc72e7";

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
