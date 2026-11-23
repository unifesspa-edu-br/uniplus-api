namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class ChavesDosItensENiveisNoEnvelopeTests
{
    [Theory]
    [InlineData("item_avaliacao", false)]
    [InlineData(null, true)]
    [InlineData("Item", true)]
    public void LerItem_ChaveDeclarada_ConfereForma(string? chave, bool falha)
    {
        LeitorEnvelope leitor = new();
        JsonObject etapa = new() { ["itensDeAvaliacao"] = new JsonArray(Item(chave)) };
        LeituraDasEtapas.LerItensDeAvaliacao(leitor, etapa, "etapa");
        leitor.Falhou.Should().Be(falha);
    }

    [Theory]
    [InlineData("nivel_atende", false)]
    [InlineData(null, true)]
    [InlineData("Nivel", true)]
    public void LerNivel_ChaveDeclarada_ConfereForma(string? chave, bool falha)
    {
        LeitorEnvelope leitor = new();
        JsonObject etapa = new() { ["escalaDeAtendimento"] = new JsonArray(Nivel(chave)) };
        LeituraDasEtapas.LerEscalaDeAtendimento(leitor, etapa, "etapa");
        leitor.Falhou.Should().Be(falha);
    }

    [Fact]
    public void LerItens_ChaveRepetida_Recusa()
    {
        LeitorEnvelope leitor = new();
        JsonObject etapa = new() { ["itensDeAvaliacao"] = new JsonArray(Item("item_a"), Item("item_a")) };
        LeituraDasEtapas.LerItensDeAvaliacao(leitor, etapa, "etapa");
        leitor.Falhou.Should().BeTrue();
    }

    [Fact]
    public void LerNiveis_ChaveRepetida_Recusa()
    {
        LeitorEnvelope leitor = new();
        JsonObject etapa = new() { ["escalaDeAtendimento"] = new JsonArray(Nivel("nivel_a"), Nivel("nivel_a")) };
        LeituraDasEtapas.LerEscalaDeAtendimento(leitor, etapa, "etapa");
        leitor.Falhou.Should().BeTrue();
    }

    [Fact]
    public void LerItens_OrdemDeclarada_RetornaChavesNaOrdem()
    {
        JsonObject primeiro = Item("item_primeiro");
        JsonObject segundo = Item("item_segundo");
        segundo["ordem"] = 2;
        LeitorEnvelope leitor = new();
        JsonObject etapa = new() { ["itensDeAvaliacao"] = new JsonArray(segundo, primeiro) };
        LeituraDasEtapas.LerItensDeAvaliacao(leitor, etapa, "etapa")
            .Select(item => item.Chave.Valor).Should().Equal("item_primeiro", "item_segundo");
        leitor.Falhou.Should().BeFalse();
    }

    [Fact]
    public void LerNiveis_OrdemDeclarada_RetornaChavesNaOrdem()
    {
        JsonObject primeiro = Nivel("nivel_primeiro");
        JsonObject segundo = Nivel("nivel_segundo");
        segundo["ordem"] = 2;
        LeitorEnvelope leitor = new();
        JsonObject etapa = new() { ["escalaDeAtendimento"] = new JsonArray(segundo, primeiro) };
        LeituraDasEtapas.LerEscalaDeAtendimento(leitor, etapa, "etapa")!
            .Select(nivel => nivel.Chave.Valor).Should().Equal("nivel_primeiro", "nivel_segundo");
        leitor.Falhou.Should().BeFalse();
    }

    private static JsonObject Item(string? chave) => new()
    {
        ["id"] = Guid.NewGuid().ToString(),
        ["chave"] = chave,
        ["ordem"] = 1,
        ["rotulo"] = "Avaliação",
        ["pontuacaoMaxima"] = "1.0000",
    };

    private static JsonObject Nivel(string? chave) => new()
    {
        ["chave"] = chave,
        ["ordem"] = 1,
        ["rotulo"] = "Atende",
        ["fracao"] = "1.0000",
    };
}
