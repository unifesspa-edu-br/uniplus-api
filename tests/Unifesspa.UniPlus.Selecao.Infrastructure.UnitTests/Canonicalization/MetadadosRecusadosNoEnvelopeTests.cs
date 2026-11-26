namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class MetadadosRecusadosNoEnvelopeTests
{
    [Fact]
    public void LerMetadadosFatos_CodigoRepetido_NaoRepeteValor()
    {
        const string valor = "EXEMPLO_CODIGO_REPETIDO";
        JsonObject item = JsonNode.Parse("""{"codigoDoDado":"EXEMPLO_CODIGO_REPETIDO","tipo":"OPCAO","origem":"DECLARADO","quantidadeDeValores":"VALOR_UNICO","faseDeDisponibilidade":"INSCRICAO","fonteDoValor":"CAMPO_FORMULARIO","opcoes":null,"opcoesDeclaradas":null}""")!.AsObject();
        JsonObject documento = new() { ["definicoesDosDados"] = new JsonArray(item, item.DeepClone()) };
        LeitorEnvelope leitor = new();
        LeituraDosMetadadosDosFatos.LerMetadadosFatos(leitor, documento);
        FieldError causa = leitor.Falha<object>().Errors.Should().ContainSingle().Subject;
        causa.Error.Code.Should().Be("EnvelopeCodec.EnvelopeMalformado");
        causa.Error.Message.Should().Contain("definicoesDosDados[1]").And.NotContainEquivalentOf(valor);
        causa.Field.Should().BeNull();
    }
}
