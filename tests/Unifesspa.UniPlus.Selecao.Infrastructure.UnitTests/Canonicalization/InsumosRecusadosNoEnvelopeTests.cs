namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class InsumosRecusadosNoEnvelopeTests
{
    [Fact]
    public void Ler_TipoDeAjusteDesconhecido_NaoRepeteValor()
    {
        const string valor = "ExemploAjusteDesconhecido";
        JsonArray documento = JsonNode.Parse("""[{"ofertaCursoOrigemId":"11111111-1111-1111-1111-111111111111","vagasOfertadasBase":40,"percentualDaReservaLegal":null,"regraDistribuicao":{"codigo":"DISTRIBUICAO_INSTITUCIONAL","versao":"v1","hash":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"},"regraAjuste":null,"ajusteDoQuadro":{"$tipo":"ExemploAjusteDesconhecido"},"referenciaDemografica":null,"grupoAreaEnem":null}]""")!.AsArray();
        LeitorEnvelope leitor = new();
        LeituraDosInsumosDaDistribuicao.Ler(leitor, documento);
        FieldError causa = leitor.Falha<object>().Errors.Should().ContainSingle().Subject;
        causa.Error.Code.Should().Be("EnvelopeCodec.EnvelopeMalformado");
        causa.Error.Message.Should().Contain("distribuicao[0].ajusteDoQuadro").And.NotContainEquivalentOf(valor);
        causa.Field.Should().BeNull();
    }
}
