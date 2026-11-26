namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class TiposDeAtoRecusadosNoEnvelopeTests
{
    [Fact]
    public void LerTiposDeAto_CodigoRepetido_NaoRepeteValor()
    {
        const string valor = "ExemploTipoRepetido";
        JsonObject documento = new() { ["tiposDeAto"] = new JsonArray(Tipo(valor), Tipo(valor)) };
        LeitorEnvelope leitor = new();
        LeituraDosTiposDeAto.LerTiposDeAto(leitor, documento, [], []);
        FieldError causa = leitor.Falha<object>().Errors.Should().ContainSingle().Subject;
        causa.Error.Code.Should().Be("EnvelopeCodec.EnvelopeMalformado");
        causa.Error.Message.Should().Contain("tiposDeAto").And.Contain("mais de uma vez").And.NotContain(valor);
        causa.Field.Should().BeNull();
    }

    private static JsonObject Tipo(string codigo) => new()
    {
        ["codigo"] = codigo,
        ["nome"] = "Tipo de exemplo",
        ["unicoPorObjeto"] = false,
        ["ehResultado"] = false,
        ["congelaConfiguracao"] = false,
    };
}
