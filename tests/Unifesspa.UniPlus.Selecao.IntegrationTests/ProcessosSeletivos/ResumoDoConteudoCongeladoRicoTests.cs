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
    private const string ResumoDoExemploRico = "cd727a833fdf4b506272acd9e6f97a782dfe917b2f127d1f16e1f2bace5b13e0";
    private const string ResumoDoExemploDeReferencia = "fbd22d0514d6da7aa5dbab1da9389f95d8faaf7e1b42ea4ee02f821194b11b59";
    private const string ResumoDoExemploComCascata = "8c0a348d3b486a6e54d2ed987bfb8bce0f77c2f16cf16108c74c723a67e24c25";

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
