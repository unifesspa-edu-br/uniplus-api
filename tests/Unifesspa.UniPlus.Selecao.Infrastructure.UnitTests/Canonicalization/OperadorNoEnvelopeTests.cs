namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class OperadorNoEnvelopeTests
{
    private static JsonElement Valor(string bruto)
    {
        using JsonDocument documento = JsonDocument.Parse(bruto);
        return documento.RootElement.Clone();
    }

    private static JsonObject Item(string operador) =>
        (JsonObject)JsonNode.Parse($$"""{"exibicao":[[{"codigoDoDado":"FATO","operador":{{operador}},"valor":true}]]}""")!;

    [Theory(DisplayName = "O envelope escreve cada operador pelo código declarado")]
    [InlineData(Operador.Igual, "IGUAL", "true")]
    [InlineData(Operador.MaiorIgual, "MAIOR_IGUAL", "18")]
    [InlineData(Operador.MenorIgual, "MENOR_IGUAL", "60")]
    [InlineData(Operador.Diferente, "DIFERENTE", "true")]
    public void SerializarDnf_Operador_EscrevePeloCodigo(Operador operador, string codigo, string valor) =>
        SerializacaoDosPredicados.SerializarDnf([(0, "FATO", operador, Valor(valor))])!.ToJsonString()
            .Should().Be($$"""[[{"codigoDoDado":"FATO","operador":"{{codigo}}","valor":{{valor}}}]]""");

    [Theory(DisplayName = "O envelope escreve os operadores de lista pelo código declarado")]
    [InlineData(Operador.Em, "EM")]
    [InlineData(Operador.NaoEm, "NAO_EM")]
    public void SerializarDnf_OperadorDeLista_EscrevePeloCodigo(Operador operador, string codigo) =>
        SerializacaoDosPredicados.SerializarDnf([(0, "FATO", operador, Valor("[\"A\"]"))])!.ToJsonString()
            .Should().Be($$"""[[{"codigoDoDado":"FATO","operador":"{{codigo}}","valor":["A"]}]]""");

    [Fact(DisplayName = "O envelope lê o código declarado")]
    public void LerDnf_CodigoDeclarado_LeOOperador()
    {
        LeitorEnvelope leitor = new();

        IReadOnlyList<(int Clausula, string Fato, Operador Operador, JsonElement Valor)> linhas = LeituraDasCondicoes.LerDnf(leitor, Item("\"DIFERENTE\""), "exibicao", "item");

        leitor.Falhou.Should().BeFalse();
        linhas.Select(static l => l.Operador).Should().Equal(Operador.Diferente);
    }

    [Theory(DisplayName = "O envelope recusa o operador que não é código declarado, sem repetir o valor")]
    [InlineData("\"NENHUMA\"")]
    [InlineData("\"igual\"")]
    [InlineData("\"QUASE_IGUAL\"")]
    [InlineData("3")]
    public void LerDnf_OperadorForaDoCatalogo_RecusaOEnvelope(string operador)
    {
        LeitorEnvelope leitor = new();

        LeituraDasCondicoes.LerDnf(leitor, Item(operador), "exibicao", "item");

        leitor.Falhou.Should().BeTrue();
        leitor.Erro.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        leitor.Erro.Message.Should().NotContain("QUASE_IGUAL").And.NotContain("NENHUMA");
    }
}
