namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// A forma que a leitura do JSON do predicado, da obrigatoriedade e do impedimento não remonta é
/// recusada com o código da forma inválida; o conversor da persistência a transforma em exceção.
/// </summary>
public sealed class PredicadoDnfJsonTests
{
    [Theory(DisplayName = "O documento escreve cada operador pelo código declarado")]
    [InlineData(Operador.Igual, "IGUAL", "true")]
    [InlineData(Operador.AlgumDe, "ALGUM_DE", "[\"A\"]")]
    [InlineData(Operador.MaiorIgual, "MAIOR_IGUAL", "18")]
    [InlineData(Operador.MenorIgual, "MENOR_IGUAL", "60")]
    [InlineData(Operador.Diferente, "DIFERENTE", "true")]
    [InlineData(Operador.NenhumDe, "NENHUM_DE", "[\"A\"]")]
    public void ParaJson_Operador_EscrevePeloCodigo(Operador operador, string codigo, string valor)
    {
        using JsonDocument documento = JsonDocument.Parse(valor);
        PredicadoDnf predicado = PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(0, CondicaoDnf.Criar("FATO", operador, documento.RootElement).Value!)]).Value!;

        PredicadoDnfJson.ParaJson(predicado).ToJsonString()
            .Should().Be($$"""[[{"fato":"FATO","operador":"{{codigo}}","valor":{{valor}}}]]""");
    }

    [Theory(DisplayName = "O documento com operador fora do catálogo é recusado no campo do operador, sem virar operador nenhum")]
    [InlineData("QUASE_IGUAL")]
    [InlineData("igual")]
    [InlineData("NENHUMA")]
    public void DeJson_OperadorForaDoCatalogo_RecusaNoCampoDoOperador(string operador)
    {
        using JsonDocument documento = JsonDocument.Parse($$"""[[{"fato":"FATO","operador":"{{operador}}","valor":true}]]""");

        Result<PredicadoDnf> lido = PredicadoDnfJson.DeJson(documento.RootElement);

        lido.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(("[0][0].operador", "CondicaoDnf.OperadorInvalido"));
    }

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
