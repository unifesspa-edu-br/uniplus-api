namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class FatosDerivadosRecusadosNoEnvelopeTests
{
    [Fact]
    public void LerFatosDerivados_CodigoRepetido_NaoRepeteValor()
    {
        const string valor = "EXEMPLO_FATO_REPETIDO";
        JsonObject fato = JsonNode.Parse("""{"codigo":"EXEMPLO_FATO_REPETIDO","nome":"Dado de exemplo","descricao":"Descrição de exemplo","componenteExibicao":null,"tipo":"NUMERICO","quantidadeDeValores":"VALOR_UNICO","classificacaoProtecao":"PESSOAL","hipoteseLegal":"CUMPRIMENTO_OBRIGACAO_LEGAL","finalidadeTratamento":"Finalidade de exemplo"}""")!.AsObject();
        JsonObject documento = new() { ["dadosCalculados"] = new JsonArray(fato, fato.DeepClone()) };
        LeitorEnvelope leitor = new();
        LeituraDosFatosDerivados.LerFatosDerivados(leitor, documento);
        FieldError causa = leitor.Falha<object>().Errors.Should().ContainSingle().Subject;
        causa.Error.Code.Should().Be("EnvelopeCodec.EnvelopeMalformado");
        causa.Error.Message.Should().Be("Envelope malformado em 'dadosCalculados': um fato aparece mais de uma vez.");
        causa.Error.Message.Should().NotContainEquivalentOf(valor);
        causa.Field.Should().BeNull();
    }
}
