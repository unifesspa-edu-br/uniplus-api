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
    private const string ResumoDoExemploRico = "3dbaf8868f089022070b300bd72bcba313a5459ea39e99549cb30245ae91c8b2";
    private const string ResumoDoExemploDeReferencia = "f4208e41272400f26358c0d8fc836859b9b664115f91ce9ba37a1ca434bb8e8b";
    private const string ResumoDoExemploComCascata = "d2a936cd3608faf1b3b936e2a28c9f05740a305c267f44bf667a9f5a3f8b8072";

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
