namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class LocalidadeRecusadaNoEnvelopeTests
{
    [Theory]
    [InlineData("fusoHorario", "ExemploFusoInvalido", "localidade.fusoHorario")]
    [InlineData("codigoIbge", "ExemploCodigoInvalido", "localidade")]
    public void LerLocalidade_ValorInvalido_NaoRepeteValor(string campo, string valor, string caminho)
    {
        JsonObject localidade = new() { ["codigoIbge"] = "1504208", ["nome"] = "Marabá", ["uf"] = "PA", ["fusoHorario"] = "America/Belem" };
        localidade[campo] = valor;
        LeitorEnvelope leitor = new();
        LeituraDoCalendario.LerLocalidade(leitor, new JsonObject { ["localidade"] = localidade });

        FieldError causa = leitor.Falha<object>().Errors.Should().ContainSingle().Subject;
        causa.Error.Code.Should().Be("EnvelopeCodec.EnvelopeMalformado");
        causa.Error.Message.Should().Contain(caminho).And.NotContain(valor);
        if (campo == "codigoIbge")
        {
            causa.Error.Message.Should().Contain("sete dígitos").And.Contain("UF correspondente");
        }
        causa.Field.Should().BeNull();
    }
}
