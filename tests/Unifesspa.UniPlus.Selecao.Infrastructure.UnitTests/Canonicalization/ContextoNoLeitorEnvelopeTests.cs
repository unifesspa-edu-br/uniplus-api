namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class ContextoNoLeitorEnvelopeTests
{
    [Fact]
    public void ExigirChaves_ContextoValido_Aceita()
    {
        LeitorEnvelope leitor = new();
        JsonObject objeto = JsonNode.Parse("""{"codigo":"A","contexto":{"codigoRotulo":"Rótulo"}}""")!.AsObject();
        leitor.ExigirChaves(objeto, "$", "codigo");
        leitor.Falhou.Should().BeFalse();
    }

    [Theory]
    [InlineData("""{"codigos":["A","B"],"contexto":{"codigosRotulos":["a","b"]}}""")]
    [InlineData("""{"codigos":[],"contexto":{"codigosRotulos":[]}}""")]
    public void ExigirChaves_ListaDeRotulosDoMesmoTamanho_Aceita(string conteudo)
    {
        LeitorEnvelope leitor = new();
        leitor.ExigirChaves(JsonNode.Parse(conteudo)!.AsObject(), "$", "codigos");
        leitor.Falhou.Should().BeFalse();
    }

    [Theory]
    [InlineData("""{"codigos":["A","B"],"contexto":{"codigosRotulos":["a"]}}""")]
    [InlineData("""{"codigos":["A"],"contexto":{"codigosRotulos":[1]}}""")]
    [InlineData("""{"codigos":["A"],"contexto":{"outrosRotulos":["a"]}}""")]
    public void ExigirChaves_ListaDeRotulosInvalida_Recusa(string conteudo)
    {
        LeitorEnvelope leitor = new();
        leitor.ExigirChaves(JsonNode.Parse(conteudo)!.AsObject(), "$", "codigos");
        leitor.Falhou.Should().BeTrue();
    }

    [Theory]
    [InlineData("""{"codigo":"A","outra":true}""")]
    [InlineData("""{"codigo":"A","contexto":null}""")]
    [InlineData("""{"codigo":"A","contexto":[]}""")]
    [InlineData("""{"codigo":"A","contexto":{"codigo": "A"}}""")]
    [InlineData("""{"codigo":"A","contexto":{"codigoRotulo": 1}}""")]
    [InlineData("""{"codigo":"A","contexto":{"intrusoRotulo":"Rótulo"}}""")]
    public void ExigirChaves_ChaveOuContextoInvalidos_Recusa(string conteudo)
    {
        LeitorEnvelope leitor = new();
        leitor.ExigirChaves(JsonNode.Parse(conteudo)!.AsObject(), "$", "codigo");
        leitor.Falhou.Should().BeTrue();
        leitor.Erro.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact]
    public void DivergeDoCongelado_SoRotuloDiferente_NaoDiverge()
    {
        JsonObject payload = JsonNode.Parse("""{"bloco":[{"codigo":"A","contexto":{"codigoRotulo":"Antigo"}}]}""")!.AsObject();
        JsonNode esperado = JsonNode.Parse("""[{"codigo":"A","contexto":{"codigoRotulo":"Novo"}}]""")!;

        LeituraDaDerivacao.DivergeDoCongelado(payload, "bloco", esperado).Should().BeFalse();
    }

    [Fact]
    public void DivergeDoCongelado_CodigoDiferente_Diverge()
    {
        JsonObject payload = JsonNode.Parse("""{"bloco":[{"codigo":"A"}]}""")!.AsObject();
        JsonNode esperado = JsonNode.Parse("""[{"codigo":"B"}]""")!;

        LeituraDaDerivacao.DivergeDoCongelado(payload, "bloco", esperado).Should().BeTrue();
    }
}
