namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class DiscriminadoresRecusadosNoEnvelopeTests
{
    [Fact]
    public void LerPredicadoObrigatoriedade_TipoDesconhecido_NaoRepeteValor()
    {
        const string valor = "ExemploValorDesconhecido";
        LeitorEnvelope leitor = new();

        LeituraDosDocumentosExigidos.LerPredicadoObrigatoriedade(
            leitor,
            new() { ["tipo"] = valor, ["parametros"] = new JsonObject() },
            "predicado");

        FieldError causa = leitor.Falha<object>().Errors.Should().ContainSingle().Subject;
        causa.Error.Code.Should().Be("EnvelopeCodec.RegraDesconhecida");
        causa.Error.Message.Should().Contain("condição de obrigatoriedade").And.NotContainEquivalentOf(valor).And.NotContain("PredicadoObrigatoriedade");
        causa.Field.Should().BeNull();
    }
}
