namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class EnvelopeLidoProvaEMotivosTests
{
    [Fact]
    public void Reidratar_ProvaEMotivos_RecodificaOsMesmosBytes()
    {
        ProcessoSeletivo processo = ExemploDeEnvelopeProcessoRico.ProcessoRico();
        SnapshotCanonico original = ExemploDeEnvelope.Canonicalizador.Canonicalizar(ExemploDeEnvelopeEntrada.Entrada(processo));
        JsonObject payload = ApoioDoEnvelopeCodec.Envelope(original);
        payload["provaPresencial"] = JsonNode.Parse("""{"temProvaPresencial":true,"municipios":[{"codigoIbge":"1504208","uf":"PA","nome":"Marabá"}]}""");
        payload["motivosDeDecisao"] = JsonNode.Parse("""[{"tipoDeDecisaoCodigo":"ANALISE_DOCUMENTAL","codigo":"A","lugar":{"tipo":"FASE","id":"11111111-1111-1111-1111-111111111111"},"descricao":"Primeiro","subclassificacao":null,"sentido":"FAVORAVEL","versaoDoEditalDeEntrada":1,"justificativaDaInclusao":null},{"tipoDeDecisaoCodigo":"ANALISE_DOCUMENTAL","codigo":"B","lugar":{"tipo":"ETAPA","id":"22222222-2222-2222-2222-222222222222"},"descricao":"Segundo","subclassificacao":null,"sentido":"FAVORAVEL","versaoDoEditalDeEntrada":1,"justificativaDaInclusao":null}]""");
        byte[] bytes = PerfilCanonicoV1.Instancia.Serializar(payload);
        EnvelopeReidratado lido = ApoioDoEnvelopeCodec.Reidratado(ExemploDeEnvelopeEntrada.VersaoDeAbertura(processo, bytes));
        lido.Configuracao.ProvaPresencial!.Municipios.Single().Nome.Should().Be("Marabá");
        lido.Configuracao.MotivosDeDecisao.Should().HaveCount(2);
        ProcessoSeletivo reconstruido = ProcessoDoGrafoLidoDeTeste.Criar(processo, lido.Configuracao);
        FidelidadeDoEnvelopeDeTeste.Recodificar(reconstruido, lido).Bytes.Should().Equal(bytes);
    }
}
