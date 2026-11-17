namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// A forma que a leitura do JSON do predicado, da obrigatoriedade e do impedimento não remonta é
/// recusada com o código da forma inválida; o conversor da persistência a transforma em exceção.
/// </summary>
public sealed class PredicadoDnfJsonTests
{
    [Theory(DisplayName = "O predicado fora da forma esperada é recusado como forma inválida")]
    [InlineData("""{"fato":"IDADE"}""")]
    [InlineData("""[[]]""")]
    [InlineData("""[[{"fato":"IDADE","operador":"IGUAL"}]]""")]
    public void DeJson_ForaDaForma_RecusaComFormaInvalida(string json)
    {
        using JsonDocument documento = JsonDocument.Parse(json);

        Result<PredicadoDnf> lido = PredicadoDnfJson.DeJson(documento.RootElement);

        lido.Error!.Code.Should().Be(PredicadoDnfJson.FormaInvalida);
    }

    [Theory(DisplayName = "A obrigatoriedade fora da forma esperada é recusada como forma inválida")]
    [InlineData("""{"predicado":null}""")]
    [InlineData("""{"tipo":"SEMPRE","predicado":[[{"fato":"PCD","operador":"IGUAL","valor":true}]]}""")]
    [InlineData("""{"tipo":"QUANDO","predicado":null}""")]
    public void ObrigatoriedadeDeJson_ForaDaForma_RecusaComFormaInvalida(string json)
    {
        using JsonDocument documento = JsonDocument.Parse(json);

        Result<Obrigatoriedade> lida = PredicadoDnfJson.ObrigatoriedadeDeJson(documento.RootElement);

        lida.Error!.Code.Should().Be(PredicadoDnfJson.FormaInvalida);
    }

    [Fact(DisplayName = "O impedimento sem mensagem é recusado como forma inválida")]
    public void ImpedimentoDeJson_SemMensagem_RecusaComFormaInvalida()
    {
        using JsonDocument documento = JsonDocument.Parse("""{"quando":[[{"fato":"PCD","operador":"IGUAL","valor":true}]]}""");

        Result<Impedimento?> lido = PredicadoDnfJson.ImpedimentoDeJson(documento.RootElement);

        lido.Error!.Code.Should().Be(PredicadoDnfJson.FormaInvalida);
    }
}
