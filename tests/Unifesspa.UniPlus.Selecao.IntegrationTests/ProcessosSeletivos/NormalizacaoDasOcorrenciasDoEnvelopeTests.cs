namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Xunit;

public sealed class NormalizacaoDasOcorrenciasDoEnvelopeTests
{
    [Fact]
    public void Canonicalizar_OcorrenciasComTextoDecomposto_EmiteEOrdenaTextoNormalizado()
    {
        ProcessoSeletivo processo = ExemploDeEnvelopeProcessoRico.ProcessoRico(comRegrasDeComprovacao: true);
        SnapshotCanonico documento = new SnapshotPublicacaoCanonicalizer().Canonicalizar(ExemploDeEnvelopeEntrada.Entrada(processo));
        JsonArray ocorrencias = ApoioDoEnvelopeCodec.Envelope(documento)["regrasDeComprovacao"]![0]!["ocorrenciasEsperadas"]!.AsArray();

        ocorrencias.Select(static item => item!.GetValue<string>()).Should().Equal("Anuário", "Análise");
    }
}
